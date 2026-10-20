# FlixLocadora

API em **.NET 10** criada do template `interview-api`: CRUD com envelope único, validação explícita, autenticação
ASP.NET Core Identity + JWT e **DataLayer database-first regenerável** a partir da connection string.

## Do banco do caso a um CRUD funcionando

```powershell
# 1. o banco: DDL em database/schema/0001_<caso>.sql (+ seed fictício em database/seed/) — ou aponte para o banco entregue
dotnet user-secrets set ConnectionStrings:AppDb "<connection string>" --project src/FlixLocadora.Api   # opcional: o padrão é LocalDB
./scripts/db-up.ps1 -Seed      # cria/atualiza o banco (DbUp)
./scripts/scaffold.ps1         # Data/Generated ← banco (T4: IEntity, ToTable explícito, uma configuração por entidade)
./scripts/crudgen.ps1          # uma fatia CRUD por tabela: contratos, regras do banco, conversor, serviço, controller, testes
dotnet test                    # ModelVsDatabase + arquitetura + fumaça (banco descartável por execução)
dotnet run --project src/FlixLocadora.Api --launch-profile http    # Scalar em /scalar
```

Depois: regras do caso nos ganchos dos serviços (`Filter`, `Order`, `OnCreating/OnUpdating`, `Check*`), cenários nos
testes, e o go-live da autenticação.

## Convenções

- **Envelope** `{ status, data, errors[{code, message, field}], requestId }` com status HTTP real: 400 forma · 401/403 auth ·
  404 · 409 unicidade/concorrência · 422 regra de negócio · 500. O status vem da categoria do `ErrorCode`.
- **Gerado × seu:** `Data/Generated` (scaffold) e `Business/*/Generated` (CrudGen) são regenerados — nunca edite; o resto
  é seu. Personalização em `partial` (`Data/Partials`, partial records dos contratos).
- **Mapperly:** campo sem par no conversor é erro de build — o compilador aponta o que uma mudança de banco afetou.
- **Banco:** `dbo` = domínio (scaffold) · `auth` = Identity (migrations: `dotnet ef migrations add <Nome> -p src/FlixLocadora.Api -c AuthDbContext -o Data/Auth/Migrations`) · `deploy` = DbUp.

## Autenticação — go-live

Os controllers gerados nascem com `[Authorize(Policy = …)]` + `[AllowAnonymous]` (o build mostra ASP0026 como lembrete).
Para ligar: apagar as linhas `[AllowAnonymous]`, tirar as rotas da seção "testing phase" de
`tests/FlixLocadora.Tests/Architecture/AnonymousEndpoints.txt` e rodar `./scripts/dev-secrets.ps1` (chave JWT + senha do admin
em user-secrets). Políticas: `Read` autenticado · `Write` Admin/Operator · `Admin`.

## Comandos úteis

| Para | Comando |
|---|---|
| ver o diagrama do banco | `docs/modelo-de-dados.md` (gerado pelo CrudGen) |
| remover fatias de tabelas que sumiram | `./scripts/crudgen.ps1 -PruneOwned` |
| testar outro servidor SQL | `$env:TEST_DB_CONNECTION = "Server=...;User Id=...;Password=..."` |
| SQL Server em container | `infra/docker-compose.yml` |
