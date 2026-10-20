namespace FlixLocadora.Api.Business.Common;

// Markers for convention-based registration (Infra/CompositionRoot). A class implementing an interface derived
// from a marker is registered in DI without touching Program.cs or the CompositionRoot — in the base projects
// every entity needed two AddScoped lines (MyFlix.API/Program.cs:34-42).

/// <summary>Application service (Scoped).</summary>
public interface IService;

/// <summary>Context of the current request, without depending on ASP.NET Core (implemented in Infra/Http).</summary>
public interface IRequestContext
{
    /// <summary>Correlation id: the X-Request-Id received, or ASP.NET's trace identifier.</summary>
    string RequestId { get; }
}
