# FlixLocadora

API de **locadora de filmes** em **.NET 10** — catálogo com estoque de cópias, clientes e **locações** (alugar,
prorrogar, devolver, cancelar), com autenticação ASP.NET Core Identity + JWT e banco **database-first** regenerável.
Criada do template `interview-api` (`C:\coding\git\_templates`); o catálogo do MyFlix (`C:\desafio\myflix_back`) é a origem.
Planejamento: `C:\coding\planejamento\07-flixlocadora.md`.

## Rodar em 3 comandos

Pré-requisitos: .NET SDK 10 e SQL Server LocalDB (Windows) — ou SQL Server em Docker (`infra/docker-compose.yml`).

```powershell
./scripts/db-up.ps1 -Seed                                    # banco FlixLocadora no LocalDB + 12 filmes, 6 clientes, 5 locações (fictícios)
./scripts/dev-secrets.ps1                                    # opcional: chave JWT fixa + senha do admin (user-secrets)
dotnet run --project src/FlixLocadora.Api --launch-profile http   # Scalar em http://localhost:5406/scalar
```

Sem o `dev-secrets.ps1`, a API sobe mesmo assim em Development (chave JWT efêmera, sem usuário admin) e avisa no log.
O arquivo `src/FlixLocadora.Api/FlixLocadora.Api.http` tem as chamadas prontas, inclusive os casos de erro do seed
(o cliente 2 tem locação atrasada).

## O que a API faz

| Recurso | Endpoints | Destaques |
|---|---|---|
| Filmes | `GET/POST /api/movies` (`?genre&available&search`), `GET/PUT/DELETE /api/movies/{id}`, `PATCH /api/movies/{id}/copies` | estoque (`TotalCopies`/`AvailableCopies`) muda **só** por instruções atômicas; filtros em SQL (o MyFlix filtrava em memória); não exclui filme com histórico (409) |
| Clientes | `GET/POST /api/customers`, `GET/PUT/DELETE /api/customers/{id}`, `GET /api/customers/{id}/rentals` | CPF com dígito verificador e único (409), e-mail único gravado em minúsculas, telefone só com dígitos |
| Locações | `GET/POST /api/rentals` (`?status&customerId&movieId&search`), `GET/PUT/DELETE /api/rentals/{id}`, `POST /api/rentals/{id}/return` | status **derivado** das datas (`Active`/`Overdue`/`Returned`); diária congelada no aluguel; multa por dia iniciado |
| Contas | `POST /api/auth/register · login`, `GET /api/auth/me`, gestão de roles | Identity (hash, lockout, roles) + JWT |

Filmes e clientes saíram do **CrudGen** (`crudgen.json`: `AvailableCopies` fora do POST; estoque fora do PUT, que vai pelo
`PATCH …/copies`). Locações são escritas à mão (`"Rental": { "skip": true }`) — é onde mora a regra.

Toda resposta é o **envelope** `{ status, data, errors[{code, message, field}], requestId }` com o **status HTTP real**:
400 forma inválida · 401/403 autenticação · 404 · 409 unicidade/vínculo · 422 regra de negócio · 500 (sem detalhes internos).

## Regras de locação

| Regra | Onde | Código (HTTP) |
|---|---|---|
| `days` de 1 a `Rental:MaxDays` | validador | `VALIDATION_FAILED` (400) |
| filme existe, ativo e com cópia — **reserva atômica** | `MovieRepository.TryReserveCopyAsync` + `CHECK` de estoque | `RENTAL_MOVIE_NOT_FOUND` / `MOVIE_UNAVAILABLE` (422) |
| cliente existe, ativo, sem locação atrasada e abaixo de `Rental:MaxActivePerCustomer` | serviço, com a linha do cliente travada | `RENTAL_CUSTOMER_NOT_FOUND` / `CUSTOMER_INACTIVE` / `CUSTOMER_HAS_OVERDUE_RENTALS` / `CUSTOMER_RENTAL_LIMIT_REACHED` (422) |
| devolução: `total = dias previstos × diária + multa`; `multa = dias de atraso (arredondados para cima) × diária × Rental:LateFeeMultiplier`; devolução antecipada não reembolsa | `RentalPricing` (função pura) | — |
| prorrogar: só locação aberta **e no prazo**; nova data depois da atual e até `MaxDays` depois do aluguel | serviço | `RENTAL_ALREADY_RETURNED` / `RENTAL_OVERDUE` / `RENTAL_DUE_DATE_NOT_LATER` / `RENTAL_MAX_DAYS_EXCEEDED` (422) |
| cancelar (lançada por engano): só aberta e dentro de `Rental:CancelWindowMinutes` | serviço | `RENTAL_ALREADY_RETURNED` / `RENTAL_CANCEL_WINDOW_EXPIRED` (422) |
| estoque não fica abaixo das cópias alugadas | `MovieRepository.TrySetTotalCopiesAsync` | `MOVIE_COPIES_IN_USE` (422) |

`RENTAL_OVERDUE` e a janela de cancelamento fecham duas brechas da multa: sem elas, prorrogar uma locação atrasada
ou cancelá-la dias depois apagaria o que o cliente deve.

Configuração (seção `Rental` do `appsettings.json`, **validada na subida** — um valor inválido derruba a API, não uma locação):
`MaxActivePerCustomer` 3 · `MaxDays` 7 · `LateFeeMultiplier` 1,5 · `CancelWindowMinutes` 30.

## Concorrência — quem decide é o banco

| Corrida | Técnica | Teste (integração) |
|---|---|---|
| dez atendentes alugam a **última cópia** juntos | um `UPDATE … WHERE AvailableCopies > 0` (ExecuteUpdate) na mesma transação do `INSERT` da locação | 1 × 201, 9 × 422 `MOVIE_UNAVAILABLE`, estoque 0 |
| seis pedidos do **mesmo cliente** chegam juntos (limite 3) | `UPDLOCK` na linha do cliente (`IRepository.FindForUpdateAsync`): as locações do mesmo cliente andam em fila; as dos outros clientes, não | 3 × 201, 3 × 422 `CUSTOMER_RENTAL_LIMIT_REACHED` |
| **clique duplo** em "devolver" | `UPDLOCK` na linha da locação: o segundo espera e encontra a locação já devolvida | 1 × 200, 4 × 422 `RENTAL_ALREADY_RETURNED`, a cópia volta uma vez só |

As transações passam pela *execution strategy* (`IUnitOfWork.InTransactionAsync`), então uma falha transitória refaz a
unidade inteira. Os testes de corrida foram conferidos por mutação: sem o `UPDLOCK`, os dois últimos falham.

## Datas

Toda data-hora da API é **UTC** e sai com o `Z` (`"rentedAt": "2026-10-20T15:04:05Z"`). O `datetime2` do SQL Server não
guarda o tipo: sem tratamento, o EF lê `Unspecified` e o JSON sai sem `Z` — um cliente em UTC-3 mostraria 3 horas de
diferença. A conversão está no EF (`Data/Partials/AppDbContext.cs`, `ConfigureConventions`), não num conversor JSON, que
apagaria o `type: string` do contrato OpenAPI. Na entrada, data com offset (`-03:00`) é convertida; sem offset, vale como UTC.

## Autenticação — ligada por etapas

Os controllers de domínio nascem com `[Authorize(Policy = …)]` **e** `[AllowAnonymous]`: como o `[AllowAnonymous]` vence,
tudo fica aberto para os testes iniciais (o build mostra o aviso ASP0026 como lembrete). Para **ligar**:

1. apagar a linha `[AllowAnonymous]` de `MoviesController`, `CustomersController` e `RentalsController`;
2. tirar `api/movies`, `api/customers` e `api/rentals` da seção "testing phase" de `tests/FlixLocadora.Tests/Architecture/AnonymousEndpoints.txt`;
3. rodar `./scripts/dev-secrets.ps1` (ou definir `Jwt__SigningKey` e `Auth__AdminPassword` no ambiente).

Políticas: `Read` = autenticado · `Write` = Admin ou Operator (o `/register` dá Operator) · `Admin` = exclusão de filmes e clientes e gestão de roles (cancelar locação é `Write`).
A política de **fallback** exige usuário autenticado: endpoint sem atributo nasce protegido.

## Banco de dados (database-first)

| Schema | Dono | Como muda |
|---|---|---|
| `dbo` | domínio (`Movie`, `Customer`, `Rental`) | novo script em `database/schema/` → `./scripts/db-up.ps1` → `./scripts/scaffold.ps1` → `./scripts/crudgen.ps1` |
| `auth` | Identity | `dotnet ef migrations add <Nome> -p src/FlixLocadora.Api -c AuthDbContext -o Data/Auth/Migrations` |
| `deploy` | journal do DbUp | automático |

`Data/Generated` (scaffold) e `Business/*/Generated` (CrudGen) são regenerados — **nunca edite**; o resto é seu
(`Data/Partials`, `Data/Repositories/MovieRepository.cs`, partial records dos contratos). O diagrama do banco fica em
`docs/modelo-de-dados.md`.

## Testes

```powershell
dotnet test       # 90 testes; cria um banco descartável no LocalDB por execução (TEST_DB_CONNECTION para outro servidor)
```

Unidade (`RentalPricing` com horários exatos, CPF/CNPJ), integração (cada regra da tabela acima, as três corridas, multa com
atraso arranjado direto no banco, datas UTC nos dois sentidos, configuração inválida derrubando a subida), arquitetura
(DI por convenção, camadas, lista de endpoints anônimos, `Data/Generated` intocado) e **ModelVsDatabase**.

## Estrutura

```
src/FlixLocadora.Api
  Program.cs · Infra/ · Controllers/                      SolutionLayer  (pipeline, composição, auth, OpenAPI, erros, opções)
  Business/{Common, Movies, Customers, Rentals, Auth}     BusinessLayer  (contratos, validadores, conversores, serviços, RentalPricing)
  Data/{Generated, Partials, Repositories, Auth}          DataLayer      (EF Core database-first + Identity)
  CodeTemplates/EFCore/*.t4                               templates do scaffold
tools/FlixLocadora.Database · tools/CrudGen              DbUp · gerador de fatias CRUD
tests/FlixLocadora.Tests                                 unidade · integração · arquitetura · ModelVsDatabase
database/ · scripts/ · infra/ · crudgen.json · docs/
```
