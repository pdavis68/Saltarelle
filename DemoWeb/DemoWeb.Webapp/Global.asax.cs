﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;

namespace DemoWeb.Webapp {
	// Note: For instructions on enabling IIS6 or IIS7 classic mode, 
	// visit http://go.microsoft.com/?LinkId=9394801

	public class MvcApplication : System.Web.HttpApplication {
		public static void RegisterRoutes(RouteCollection routes) {
			routes.IgnoreRoute("{resource}.axd/{*pathInfo}");

			routes.MapRoute("AssemblyScripts", "AssemblyScripts/{assemblyName}.js", new { controller = "SaltarelleRuntime", action = "AssemblyScript" });

			routes.MapRoute(
				"HomeShortcut",              // Route name
				"{action}",                  // URL with parameters
				new { controller = "Home" }  // Parameter defaults
			);

			routes.MapRoute(
				"Default",                                              // Route name
				"{controller}/{action}/{id}",                           // URL with parameters
				new { controller = "Home", action = "Index", id = "" }  // Parameter defaults
			);

		}

		protected void Application_Start() {
			RegisterRoutes(RouteTable.Routes);
//			RouteDebug.RouteDebugger.RewriteRoutesForTesting(RouteTable.Routes);
		}
	}
}