# Modern Web App - .NET 8 Migration

This is the **modernized version** of LegacyWebApp, migrated from ASP.NET Framework 4.8 to ASP.NET Core 8.0.

## Migration Summary

### What Changed

| Component | Legacy (.NET Framework 4.8) | Modern (.NET 8) |
|-----------|----------------------------|-----------------|
| **Project Format** | Traditional .csproj with assembly references | SDK-style .csproj |
| **Configuration** | Web.config + ConfigurationManager | appsettings.json + IConfiguration |
| **Application Startup** | Global.asax with Application_Start | Program.cs with WebApplicationBuilder |
| **HTTP Context** | HttpContext.Current (static) | IHttpContextAccessor (injected) |
| **Caching** | System.Web.Caching.Cache | IMemoryCache |
| **Server Info** | Server.MapPath() | IWebHostEnvironment |
| **Dependency Management** | Manual instantiation | Built-in dependency injection |
| **Logging** | System.Diagnostics.Trace | ILogger<T> |
| **NuGet Packages** | packages.config | PackageReference in .csproj |
| **Session State** | Web.config <sessionState> | AddSession() middleware |
| **Platform** | Windows-only | Cross-platform (Linux, Windows, macOS) |

### Files Changed

**Removed:**
- `Global.asax` / `Global.asax.cs` → Replaced by `Program.cs`
- `Web.config` → Replaced by `appsettings.json`
- `packages.config` → Integrated into .csproj
- `Properties/AssemblyInfo.cs` → Generated automatically

**Added:**
- `Program.cs` - Application entry point and service configuration
- `appsettings.json` - Configuration settings
- `_ViewImports.cshtml` - Global view imports
- `_ViewStart.cshtml` - Layout configuration
- `Dockerfile` - Container image definition
- `openshift-deployment.yaml` - Kubernetes/OpenShift deployment
- `openshift-ossm-deployment.yaml` - Service Mesh weighted routing deployment (optional)
- `build-and-deploy.sh` - Deployment script

**Modified:**
- `Controllers/*.cs` - Now use constructor injection
- `Services/CustomerService.cs` - Uses IMemoryCache and IConfiguration
- `Models/Customer.cs` - Added nullable reference types
- `Views/Shared/_Layout.cshtml` - Uses Tag Helpers

## Running Locally

### Prerequisites
- .NET 8 SDK: https://dotnet.microsoft.com/download/dotnet/8.0

### Run the application
```bash
cd ModernWebApp
dotnet restore
dotnet run
```

Navigate to: http://localhost:5000

## Building for Containers

### Build Docker/Podman image
```bash
podman build -t modern-webapp:latest .
```

### Run in container
```bash
podman run -p 8080:8080 modern-webapp:latest
```

Navigate to: http://localhost:8080

## Deploying to OpenShift

### Option 1: Using Source-to-Image (S2I)
```bash
oc new-app dotnet:8.0~https://github.com/your-repo/mta-dotnet-demo \
  --context-dir=ModernWebApp \
  --name=modern-webapp

oc expose service/modern-webapp
```

### Option 2: Using Dockerfile
```bash
# Build in OpenShift
oc new-build --name=modern-webapp --binary --strategy=docker
oc start-build modern-webapp --from-dir=. --follow

# Deploy
oc new-app modern-webapp
oc expose service/modern-webapp
```

### Option 3: Using the deployment script
```bash
chmod +x build-and-deploy.sh
export OPENSHIFT_NAMESPACE=your-namespace
./build-and-deploy.sh
```

### Option 4: Using YAML manifests
```bash
# Update namespace in openshift-deployment.yaml
sed -i 's/your-namespace/my-project/g' openshift-deployment.yaml

# Apply
oc apply -f openshift-deployment.yaml
```

## Configuration

### Environment Variables

The application can be configured via environment variables (useful for OpenShift):

```bash
# Override appsettings values
ASPNETCORE_ENVIRONMENT=Production
AppSettings__AppName="My Custom Portal"
AppSettings__MaxCustomers=5000
AppSettings__EnableCaching=true
ConnectionStrings__DefaultConnection="Server=mydb;Database=customers;..."
```

### ConfigMap Example

```yaml
apiVersion: v1
kind: ConfigMap
metadata:
  name: modern-webapp-config
data:
  AppSettings__AppName: "Production Customer Portal"
  AppSettings__MaxCustomers: "10000"
  AppSettings__EnableCaching: "true"
```

Mount in deployment:
```yaml
envFrom:
  - configMapRef:
      name: modern-webapp-config
```

## Service Mesh Weighted Routing (Optional)

Use OpenShift Service Mesh 3.x to route traffic through a single URL, splitting requests between the Legacy and Modern applications using weighted routing. This enables canary-style deployments where most traffic goes to the Legacy app while a subset is directed to the Modern app for testing and performance monitoring.

### Prerequisites
- Red Hat OpenShift Service Mesh 3 operator installed on the cluster
- Kiali operator (`kiali-ossm`) installed on the cluster
- LegacyWebApp deployed in the `dotnet-legacy` namespace
- ModernWebApp deployed in the `dotnet-modern` namespace

### Deploy Service Mesh and Weighted Routing

```bash
# Apply all Service Mesh resources (namespaces, Istio control plane, gateway, routing)
oc apply -f openshift-ossm-deployment.yaml

# Enable sidecar injection on the Modern app
oc label namespace dotnet-modern istio-injection=enabled
oc rollout restart deployment/dotnet-modern -n dotnet-modern
```

This deploys:
- **Istio control plane** (IstioCNI + Istio) via the Sail operator
- **Kubernetes Gateway API** resources (Gateway, HTTPRoute, ReferenceGrants)
- **OpenShift Route** for external access with TLS edge termination
- **Kiali** for service mesh observability (traffic graph, metrics, health)
- **OSSMConsole** plugin for integrated Kiali views in the OpenShift web console

The default traffic split is **90% Legacy / 10% Modern**.

### Adjusting the Traffic Split

Edit the HTTPRoute weights to change the distribution:

```bash
oc edit httproute dotnet-weighted-route -n istio-system
```

Change the `weight` values under `backendRefs` (e.g., `50`/`50` for an even split, or `0`/`100` to send all traffic to Modern).

## Key Improvements

### 1. Cross-Platform
- Runs on Linux containers (smaller, more efficient)
- No Windows Server licensing costs
- Better cloud-native support

### 2. Modern Development
- Faster build times with SDK-style projects
- Built-in dependency injection
- Structured logging with log levels
- Better testability

### 3. Performance
- Async/await throughout the stack
- Optimized middleware pipeline
- Smaller runtime footprint

### 4. Security
- Runs as non-root user (UID 1001)
- No privilege escalation
- Modern TLS support
- Security headers built-in

### 5. Cloud-Ready
- 12-factor app principles
- Environment-based configuration
- Health checks (liveness/readiness probes)
- Graceful shutdown
- Horizontal scaling support

## Comparison: Before and After

### Code Example: Configuration

**Before (.NET Framework):**
```csharp
using System.Configuration;

var maxCustomers = int.Parse(ConfigurationManager.AppSettings["MaxCustomers"]);
```

**After (.NET 8):**
```csharp
public CustomerService(IConfiguration configuration)
{
    _maxCustomers = configuration.GetValue<int>("AppSettings:MaxCustomers");
}
```

### Code Example: Caching

**Before (.NET Framework):**
```csharp
if (HttpContext.Current.Cache["CustomerList"] != null)
{
    return (List<Customer>)HttpContext.Current.Cache["CustomerList"];
}
HttpContext.Current.Cache.Insert("CustomerList", customers, null,
    DateTime.Now.AddMinutes(5), System.Web.Caching.Cache.NoSlidingExpiration);
```

**After (.NET 8):**
```csharp
if (_cache.TryGetValue("CustomerList", out List<Customer>? cachedCustomers))
{
    return cachedCustomers;
}
var cacheOptions = new MemoryCacheEntryOptions()
    .SetAbsoluteExpiration(TimeSpan.FromMinutes(5));
_cache.Set("CustomerList", customers, cacheOptions);
```

## Troubleshooting

### Port 8080 vs 5000
OpenShift uses port 8080 by default. The Dockerfile sets `ASPNETCORE_URLS=http://+:8080`.

For local development, .NET defaults to port 5000 (HTTP) and 5001 (HTTPS).

### Database Connections
Update connection string in `appsettings.json` or set via environment variable:
```bash
ConnectionStrings__DefaultConnection="Server=mydb;Database=..."
```

### Logs
```bash
# View logs in OpenShift
oc logs -f deployment/modern-webapp

# Or using pod name
oc logs -f pod/modern-webapp-xxx
```

## Next Steps

1. Add database persistence (Entity Framework Core)
2. Add authentication/authorization
3. Add OpenAPI/Swagger documentation
4. Add integration tests
5. Set up CI/CD pipeline
6. Add monitoring (Prometheus metrics)
7. Add distributed caching (Redis)
