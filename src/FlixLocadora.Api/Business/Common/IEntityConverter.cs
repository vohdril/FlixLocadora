namespace FlixLocadora.Api.Business.Common;

/// <summary>
/// The conversions of a CRUD, in the Foundation.API spirit (converters are PURE: object in, object out, no I/O).
/// The "way back" of a CRUD is to APPLY the request onto the tracked entity — never to build a new one: the
/// base projects did that and every PUT overwrote the registration date.
/// Implementations are Mapperly mappers (code generated at compile time; a field without a counterpart is a
/// build error — see .editorconfig).
/// </summary>
public interface IEntityConverter<TEntity, in TCreate, in TUpdate, out TResponse>
    where TEntity : class
{
    TEntity ToEntity(TCreate request);

    void Apply(TUpdate request, TEntity entity);

    TResponse ToResponse(TEntity entity);
}
