# MTA .NET Framework to .NET 8 Migration Demo

This repository demonstrates migrating a legacy ASP.NET Framework 4.8 application to .NET 8 using Red Hat's Migration Toolkit for Applications (MTA).

## Demo Application: LegacyWebApp

A simple ASP.NET MVC 5 customer management application with common migration pain points:

### Key Migration Challenges

1. **System.Web Dependencies**
   - `HttpContext.Current` usage in CustomerService.cs
   - `Server.MapPath()` in HomeController.cs
   - Session state management in Global.asax.cs
   - System.Web.Caching

2. **Configuration System**
   - Web.config with appSettings and connectionStrings
   - ConfigurationManager usage throughout the code
   - Needs migration to appsettings.json and IConfiguration

3. **Project Structure**
   - Traditional .csproj (non-SDK style)
   - packages.config for NuGet dependencies
   - Global.asax for application lifecycle

4. **Outdated Dependencies**
   - Newtonsoft.Json 11.0.1 (can update to System.Text.Json or latest Newtonsoft)
   - ASP.NET MVC 5.2.7 (needs migration to ASP.NET Core MVC)
   - Microsoft.AspNet.Web.Optimization 1.1.3

5. **View Engine**
   - Razor views with System.Web.Mvc references
   - Views/web.config for Razor configuration

## Demo Flow

### Phase 1: Assessment with MTA
1. Run MTA analysis on the LegacyWebApp
2. Review generated report showing:
   - Incompatible APIs (System.Web.*)
   - Configuration migration needs
   - NuGet package updates
   - Effort estimates

### Phase 2: Migration Steps
1. Convert to SDK-style project
2. Update target framework to .NET 8
3. Replace System.Web dependencies with ASP.NET Core equivalents:
   - HttpContext → IHttpContextAccessor
   - ConfigurationManager → IConfiguration
   - Global.asax → Program.cs/Startup.cs
   - System.Web.Caching → IMemoryCache
4. Update NuGet packages
5. Migrate configuration to appsettings.json
6. Update dependency injection

### Phase 3: Containerization
1. Create Dockerfile
2. Build container image
3. Run on Linux
4. Deploy to OpenShift/Kubernetes

## Project Structure

```
LegacyWebApp/
├── App_Start/
│   └── RouteConfig.cs
├── Controllers/
│   ├── HomeController.cs
│   └── CustomerController.cs
├── Models/
│   └── Customer.cs
├── Services/
│   └── CustomerService.cs
├── Views/
│   ├── Home/
│   │   └── Index.cshtml
│   ├── Customer/
│   │   └── List.cshtml
│   └── Shared/
│       └── _Layout.cshtml
├── Global.asax
├── Global.asax.cs
├── Web.config
└── LegacyWebApp.csproj
```

## Migration Pain Points Demonstrated

| Issue | Location | Migration Path |
|-------|----------|----------------|
| HttpContext.Current | CustomerService.cs:35, 42 | IHttpContextAccessor |
| ConfigurationManager | CustomerService.cs:16, HomeController.cs:13 | IConfiguration + appsettings.json |
| Server.MapPath | HomeController.cs:16 | IWebHostEnvironment |
| Session state | Global.asax.cs:23 | ASP.NET Core Session middleware |
| System.Web.Caching | CustomerService.cs:35-47 | IMemoryCache |
| Global.asax | Global.asax.cs | Program.cs + middleware |
| packages.config | packages.config | PackageReference in .csproj |

## Next Steps

After creating this demo app, you would:
1. Set up MTA and run analysis
2. Create the migrated version
3. Create a Dockerfile
4. Document the before/after comparison
5. Prepare presentation materials highlighting time saved and issues caught
