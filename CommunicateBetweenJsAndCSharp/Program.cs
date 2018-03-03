﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CommunicateBetweenJsAndCSharp
{
	using NetDimension.NanUI;

	static class Program
	{
		/// <summary>
		/// 应用程序的主入口点。
		/// </summary>
		[STAThread]
		static void Main()
		{
			Application.EnableVisualStyles();
			Application.SetCompatibleTextRenderingDefault(false);

			if (Bootstrap.Load())
			{
				Bootstrap.RegisterAssemblyResources(System.Reflection.Assembly.GetExecutingAssembly());
				Application.Run(new Form1());
			}
		}
	}
}
