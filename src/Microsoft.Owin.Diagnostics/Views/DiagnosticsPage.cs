﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.18207
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace Microsoft.Owin.Diagnostics.Views {
    
    #line 1 "C:\Users\lodejard\Projects\temp2\katana-working\src\Microsoft.Owin.Diagnostics\Views\DiagnosticsPage.cshtml"
    using System;
    
    #line default
    #line hidden
    
    
    public class DiagnosticsPage : Microsoft.Owin.Diagnostics.Views.BaseView {
        
#line hidden
        
        public DiagnosticsPage() {
        }
        
        public override void Execute() {
            
            #line 2 "C:\Users\lodejard\Projects\temp2\katana-working\src\Microsoft.Owin.Diagnostics\Views\DiagnosticsPage.cshtml"
  
    Response.ContentType = "text/html";
    string[] crash;
    if (Request.GetQuery().TryGetValue("crash", out crash))
    {
        throw new InvalidOperationException(string.Format("User requested error '{0}'", String.Join(",", crash)));
    }

            
            #line default
            #line hidden
WriteLiteral("\r\n<!DOCTYPE html>\r\n\r\n<html");

WriteLiteral(" lang=\"en\"");

WriteLiteral(" xmlns=\"http://www.w3.org/1999/xhtml\"");

WriteLiteral(">\r\n<head>\r\n    <meta");

WriteLiteral(" charset=\"utf-8\"");

WriteLiteral(" />\r\n    <title>Microsoft OWIN Diagnostics Page</title>\r\n</head>\r\n<body>\r\n    You" +
"r site is working\r\n</body>\r\n</html>\r\n");

        }
    }
}

