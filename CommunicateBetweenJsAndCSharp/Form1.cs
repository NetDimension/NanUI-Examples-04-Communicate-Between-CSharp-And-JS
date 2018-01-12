using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CommunicateBetweenJsAndCSharp
{
	using Chromium;
	using Chromium.Remote;
	using NetDimension.NanUI;
	using System.Threading;

	public partial class Form1 : Formium
	{
		public Form1()
			: base("http://res.app.local/www/index.html", false)
		{
			InitializeComponent();

			LoadHandler.OnLoadEnd += LoadHandler_OnLoadEnd;

			//register the "my" object
			var myObject = GlobalObject.AddObject("my");

			//add property "name" to my, you should implemnt the getter/setter of name property by using PropertyGet/PropertySet events.
			var nameProp = myObject.AddDynamicProperty("name");
			nameProp.PropertyGet += (prop, args) =>
			{
				// getter - if js code "my.name" executes, it'll get the string "NanUI". 
				args.Retval = CfrV8Value.CreateString("NanUI");
				args.SetReturnValue(true);
			};
			nameProp.PropertySet += (prop, args) =>
			{
				// setter's value from js context, here we do nothing, so it will store or igrone by your mind.
				var value = args.Value;
				args.SetReturnValue(true);
			};


			//add a function showCSharpMessageBox
			var showMessageBoxFunc = myObject.AddFunction("showCSharpMessageBox");
			showMessageBoxFunc.Execute += (func, args) =>
			{
				//it will be raised by js code "my.showCSharpMessageBox(`some text`)" executed.
				//get the first string argument in Arguments, it pass by js function.
				var stringArgument = args.Arguments.FirstOrDefault(p => p.IsString);

				if (stringArgument != null)
				{
					MessageBox.Show(this, stringArgument.StringValue, "C# Messagebox", MessageBoxButtons.OK, MessageBoxIcon.Information);

					
				}
			};

			//add a function getArrayFromCSharp, this function has an argument, it will combind C# string array with js array and return to js context.
			var friends = new string[] { "Mr.JSON", "Mr.Lee", "Mr.BONG" };

			var getArrayFromCSFunc = myObject.AddFunction("getArrayFromCSharp");

			getArrayFromCSFunc.Execute += (func, args) =>
			{
				var jsArray = args.Arguments.FirstOrDefault(p => p.IsArray);



				if (jsArray == null)
				{
					jsArray = CfrV8Value.CreateArray(friends.Length);
					for (int i = 0; i < friends.Length; i++)
					{
						jsArray.SetValue(i, CfrV8Value.CreateString(friends[i]));
					}
				}
				else
				{
					var newArray = CfrV8Value.CreateArray(jsArray.ArrayLength + friends.Length);

					for (int i = 0; i < jsArray.ArrayLength; i++)
					{
						newArray.SetValue(i, jsArray.GetValue(i));
					}

					var jsArrayLength = jsArray.ArrayLength;

					for (int i = 0; i < friends.Length; i++)
					{
						newArray.SetValue(i + jsArrayLength, CfrV8Value.CreateString(friends[i]));
					}


					jsArray = newArray;
				}


				//return the array to js context

				args.SetReturnValue(jsArray);

				//in js context, use code "my.getArrayFromCSharp()" will get an array like ["Mr.JSON", "Mr.Lee", "Mr.BONG"]
			};

			//add a function getObjectFromCSharp, this function has no arguments, but it will return a Object to js context.
			var getObjectFormCSFunc = myObject.AddFunction("getObjectFromCSharp");
			getObjectFormCSFunc.Execute += (func, args) =>
			{
				//create the CfrV8Value object and the accssor of this Object.
				var jsObjectAccessor = new CfrV8Accessor();
				var jsObject = CfrV8Value.CreateObject(jsObjectAccessor);

				//create a CfrV8Value array
				var jsArray = CfrV8Value.CreateArray(friends.Length);

				for (int i = 0; i < friends.Length; i++)
				{
					jsArray.SetValue(i, CfrV8Value.CreateString(friends[i]));
				}

				jsObject.SetValue("libName", CfrV8Value.CreateString("NanUI"), CfxV8PropertyAttribute.ReadOnly);
				jsObject.SetValue("friends", jsArray, CfxV8PropertyAttribute.DontDelete);


				args.SetReturnValue(jsObject);

				//in js context, use code "my.getObjectFromCSharp()" will get an object like { friends:["Mr.JSON", "Mr.Lee", "Mr.BONG"], libName:"NanUI" }
			};


			//add a function with callback

			var callbackTestFunc = GlobalObject.AddFunction("callbackTest");
			callbackTestFunc.Execute += (func,args)=> {
				var callback = args.Arguments.FirstOrDefault(p => p.IsFunction);
				if(callback != null)
				{
					var callbackArgs = CfrV8Value.CreateObject(new CfrV8Accessor());
					callbackArgs.SetValue("success", CfrV8Value.CreateBool(true), CfxV8PropertyAttribute.ReadOnly);
					callbackArgs.SetValue("text", CfrV8Value.CreateString("Message from C#"), CfxV8PropertyAttribute.ReadOnly);

					callback.ExecuteFunction(null, new CfrV8Value[] { callbackArgs });
				}
			};


			//add a function with async callback
			var asyncCallbackTestFunc = GlobalObject.AddFunction("asyncCallbackTest");
			asyncCallbackTestFunc.Execute += async (func, args) => {
				//save current context
				var v8Context = CfrV8Context.GetCurrentContext();
				var callback = args.Arguments.FirstOrDefault(p => p.IsFunction);

				//simulate async methods.
				await Task.Delay(5000);

				if (callback != null)
				{
					//get render process context
					var rc = callback.CreateRemoteCallContext();

					//enter render process
					rc.Enter();

					//create render task
					var task = new CfrTask();
					task.Execute += (_, taskArgs) =>
					{
						//enter saved context
						v8Context.Enter();

						//create callback argument
						var callbackArgs = CfrV8Value.CreateObject(new CfrV8Accessor());
						callbackArgs.SetValue("success", CfrV8Value.CreateBool(true), CfxV8PropertyAttribute.ReadOnly);
						callbackArgs.SetValue("text", CfrV8Value.CreateString("Message from C#"), CfxV8PropertyAttribute.ReadOnly);

						//execute callback
						callback.ExecuteFunction(null, new CfrV8Value[] { callbackArgs });


						v8Context.Exit();

						//lock task from gc
						lock (task)
						{
							Monitor.PulseAll(task);
						}
					};

					lock (task)
					{
						//post task to render process
						v8Context.TaskRunner.PostTask(task);
					}

					rc.Exit();

					GC.KeepAlive(task);
				}


			};
		}



		private void LoadHandler_OnLoadEnd(object sender, Chromium.Event.CfxOnLoadEndEventArgs e)
		{
			// Check if it is the main frame when page has loaded.
			if (e.Frame.IsMain)
			{
				//Open DevTools window to watch js console output messages.
				Chromium.ShowDevTools();

				//call js function without return value
				ExecuteJavascript("sayHello()");

				//call js function with return value
				EvaluateJavascript("sayHelloToSomeone('C#')", (value, exception) =>
				{
					if (value.IsString)
					{
						// Get value from Javascript.
						var jsValue = value.StringValue;

						MessageBox.Show(this, jsValue);
					}
				});
			}
		}
	}
}
