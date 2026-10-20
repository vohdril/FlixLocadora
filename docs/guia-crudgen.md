# Guia do CrudGen — do banco novo ao CRUD, e o que fazer quando os nomes mudam

> **Para que serve este guia:** usar o FlixLocadora (ou qualquer projeto do template `interview-api`) com uma base de dados
> **nova**, **renomear tabelas** num projeto que já existe ou **reiniciar o mapeamento** num schema novo — sabendo, em cada
> passo, o que é automático e onde você opera à mão.
>
> **Como foi escrito:** cada cenário foi **ensaiado em 20/10/2026** numa cópia descartável, e os números e mensagens abaixo são
> a saída real. Onde o texto é roteiro recomendado (e não ensaio), ele diz.

## Sumário

1. [O mapa: três ferramentas em fila](#1-o-mapa-três-ferramentas-em-fila)
2. [Problemas que o CrudGen resolve](#2-problemas-que-o-crudgen-resolve-antes-de-qualquer-comando)
3. [Conceitos essenciais](#3-conceitos-essenciais--com-o-porquê-de-cada-um)
4. [Passo a passo A — uma base nova](#4-passo-a-passo-a--uma-base-nova)
5. [Passo a passo B — renomear tabelas](#5-passo-a-passo-b--renomear-tabelas-num-projeto-que-já-existe)
6. [Passo a passo C — reiniciar o mapeamento num schema novo](#6-passo-a-passo-c--reiniciar-o-mapeamento-num-schema-novo)
7. [Passo a passo D — trocar de domínio neste repositório](#7-passo-a-passo-d--trocar-de-domínio-neste-repositório)
8. [Nomes em português](#8-nomes-em-português--o-que-sai-hoje-e-como-lidar)
9. [Onde operar — resumo](#9-onde-operar--resumo)
10. [Problemas comuns](#10-problemas-comuns)
11. [Roteiro de investigação e próximo passo](#11-roteiro-de-investigação-e-próximo-passo)

---

## 1. O mapa: três ferramentas em fila

O CrudGen não trabalha sozinho. Ele é a terceira de três ferramentas, e cada uma lê uma coisa e escreve outra — entender
essa fila é o que permite saber, quando algo quebra, **em qual das três** mexer.

| # | Ferramenta | Lê | Escreve | De quem é o que ela escreve |
|---|---|---|---|---|
| 1 | [`scripts/db-up.ps1`](../scripts/db-up.ps1) (DbUp, [`tools/FlixLocadora.Database`](../tools/FlixLocadora.Database)) | `database/schema/*.sql` (e `database/seed/*.sql` com `-Seed`) | o **banco** (`dbo`) e o journal `deploy.SchemaVersions` | os scripts são seus; o banco é a **fonte da verdade** |
| 2 | [`scripts/scaffold.ps1`](../scripts/scaffold.ps1) (`dotnet ef dbcontext scaffold` + templates T4) | o banco (`dbo`, ou os schemas de `-Schemas`) | [`src/FlixLocadora.Api/Data/Generated/`](../src/FlixLocadora.Api/Data/Generated) — entidades, configurações, contexto | do gerador: **nunca edite**; o seu vai em `Data/Partials` |
| 3 | [`scripts/crudgen.ps1`](../scripts/crudgen.ps1) ([`tools/CrudGen`](../tools/CrudGen)) | o banco (o **mesmo modelo** que o scaffold lê) e o [`crudgen.json`](../crudgen.json) | uma **fatia** por tabela, o diagrama [`docs/modelo-de-dados.md`](modelo-de-dados.md) e a lista de endpoints anônimos | `*.g.cs`: do gerador (reescritos sempre); o resto: **seu** (escrito uma vez) |

Uma imagem fiel ao mecanismo: o banco é a **planta**; o scaffold ergue a **estrutura** (entidades) e o CrudGen instala
**portas e janelas padrão** (o CRUD). Toda vez que a planta muda, a estrutura e os batentes são refeitos a partir dela. Os
**móveis** — suas regras, nos arquivos que o CrudGen criou uma vez e nunca mais toca — continuam onde você os pôs. Se uma
parede muda de lugar (uma tabela renomeada), o móvel que estava encostado nela precisa ser movido por você: é o **compilador**
quem aponta qual (`arquivo:linha`).

> **No nosso caso:** `Movie` e `Customer` são fatias do CrudGen, com regras suas nos arquivos "seus" (estoque atômico, filtros,
> normalização de CPF). `Rental` é escrita à mão — o `crudgen.json` manda pular (`"Rental": { "skip": true }`), porque locação
> é operação de negócio, não cadastro.

---

## 2. Problemas que o CrudGen resolve (antes de qualquer comando)

### O problema: CRUD repetitivo, contra o relógio

Um estudo de caso de entrevista chega com um banco de 5 tabelas e algumas horas de prazo. À mão, cada tabela pede uns
**nove arquivos** — entidade, configuração, três contratos (criar, atualizar, resposta), validadores, conversor, serviço,
controller e testes —, quase 50 arquivos de código repetitivo antes da primeira regra de negócio. E repetição à mão diverge:
a coluna tem `nvarchar(200)` e o validador diz 100; um campo novo entra na entidade e é esquecido no conversor. Os
projetos-base que deram origem a estes modelos mostram exatamente isso: o conversor do Fornecedores ([vohdril/Playmove](https://github.com/vohdril/Playmove))
perdia a cidade e o estado em silêncio, e o `PUT` montava uma entidade nova que sobrescrevia a data de cadastro.

### A alternativa rejeitada

| Alternativa | Por que não resolve |
|---|---|
| Escrever tudo à mão | ~9 arquivos por tabela; as regras do banco (tamanhos, obrigatórios, FKs, `UNIQUE`) copiadas à mão divergem |
| Scaffolding de controllers do Visual Studio (`dotnet aspnet-codegenerator`) | gera um controller em cima do `DbContext`, sem envelope, sem validação e sem camadas — e não sabe regenerar sem apagar o seu |
| Mapeamento por reflexão em runtime (AutoMapper) | o campo esquecido aparece em produção, não no build; e a biblioteca passou a ter licença comercial em 2025 |
| Gerador que sobrescreve tudo a cada execução | apaga as suas regras na segunda rodada |
| Gerar a partir de um contrato OpenAPI | serve quando o **contrato** vem primeiro; num estudo de caso, o que chega primeiro é o **banco** |

O CrudGen junta o melhor das duas pontas: o que é **forma** (colunas, tamanhos, chaves, unicidade) sai do banco e é
regenerado sempre; o que é **regra** fica em arquivos seus, que ele cria uma vez e nunca mais toca. E o conversor é gerado
em tempo de compilação pelo [Mapperly](https://mapperly.riok.app/docs/intro/): um campo sem par vira **erro de build**
(`RMG012`/`RMG020`, promovidos a erro no `.editorconfig`), não um dado perdido.

### A visão de mercado

- **"O banco é a fonte, o código derivado se regenera"** é um padrão de todo ecossistema: a engenharia reversa do EF Core
  ([docs](https://learn.microsoft.com/ef/core/managing-schemas/scaffolding/)) e o
  [EF Core Power Tools](https://github.com/ErikEJ/EFCorePowerTools) no .NET, o gerador do [jOOQ](https://www.jooq.org/doc/latest/manual/code-generation/)
  no Java, o [sqlc](https://docs.sqlc.dev/) no Go. Aparece onde o banco tem dono (DBA, sistema legado, banco entregue pelo
  cliente) e a aplicação se adapta a ele.
- **O corte "gerado × seu"** é o mesmo das `partial class` do EF e dos *source generators* do .NET: o gerador escreve uma
  metade, você a outra, e um nunca apaga o outro.
- **Custo e limite:** o gerador entende a **forma**, não a **regra** — validação de CPF, estoque, multa e concorrência
  continuam sendo seu trabalho. Os nomes seguem regras do inglês (veja a [seção 8](#8-nomes-em-português--o-que-sai-hoje-e-como-lidar)).
  E todo gerador cobra disciplina: editar um arquivo gerado é perder a edição na rodada seguinte.

> **No nosso caso:** o ensaio da seção 4 levou um banco de 4 tabelas a um CRUD com **58 testes verdes** em cerca de 65 s.

---

## 3. Conceitos essenciais — com o porquê de cada um

**Fatia (slice).** É o conjunto de arquivos de uma tabela, do contrato ao teste. Para a tabela `Movie`, a fatia é:
`Business/Movies/Generated/MovieContracts.g.cs` (records `CreateMovieRequest`, `UpdateMovieRequest`, `MovieResponse` e a
classe `MovieListQuery`), `MovieDatabaseRules.g.cs` (validações que o banco determina), `MovieConstraints.g.cs` (erros
tipados das constraints), e os arquivos seus `MovieValidators.cs`, `MovieConverter.cs`, `MovieService.cs`,
`Controllers/MoviesController.cs` e `tests/FlixLocadora.Tests/Api/MoviesApiTests.cs`. O problema que a fatia resolve é a
**consistência**: as oito peças nascem do mesmo modelo do banco, então não divergem entre si.

**Gerado × seu.** Os `*.g.cs` começam com `// <auto-generated>` e são **reescritos em toda execução** — um teste de
arquitetura ([`GeneratedCodeTests`](../tests/FlixLocadora.Tests/Architecture/GeneratedCodeTests.cs)) falha se alguém criar ou
editar um arquivo sem esse cabeçalho numa pasta `Generated`. Os arquivos seus começam com
`// Created by CrudGen — this file is yours` e são escritos **só quando não existem**. Consequência prática: para
**regenerar** um arquivo seu que ainda não tem personalização (por exemplo, um conversor que quebrou depois de renomear
uma tabela), **apague-o e rode o CrudGen de novo** — ele recria o que falta. Existe também o `--force` da ferramenta
(`dotnet run --project tools/CrudGen -- --force`), que **sobrescreve** os arquivos seus — o `crudgen.ps1` não o repassa, e
vale tratá-lo como último recurso, porque ele apaga as suas regras.

**Os nomes derivados.** Um nome só, o da tabela, vira seis. O CrudGen lê o modelo com o mesmo pluralizador do `dotnet ef`
(regras do **inglês**) e deriva o resto com regras simples ([`tools/CrudGen/Naming.cs`](../tools/CrudGen/Naming.cs)):

| Tabela | Entidade (EF: singular) | Feature, pasta, controller (`Naming.Plural`) | Rota | Prefixo de erro |
|---|---|---|---|---|
| `Movie` | `Movie` | `Movies` | `api/movies` | `MOVIE_` |
| `PurchaseOrder` | `PurchaseOrder` | `PurchaseOrders` | `api/purchase-orders` | `PURCHASE_ORDER_` |
| `Category` | `Category` | `Categories` (`y` → `ies`) | `api/categories` | `CATEGORY_` |
| `Autores` (ensaio) | `Autore` | `Autores` | `api/autores` | `AUTORE_` |
| `Locacoes` (ensaio) | `Locaco` | `Locacos` | `api/locacos` | `LOCACO_` |
| `Locacao` (ensaio) | `Locacao` | `Locacaos` | `api/locacaos` | `LOCACAO_` |

As três últimas linhas são saída real com nomes em português — a [seção 8](#8-nomes-em-português--o-que-sai-hoje-e-como-lidar)
explica por que e o que fazer.

**O que o banco decide no contrato.** O `Create…Request` leva as colunas que o cliente informa: **fica de fora** a chave
gerada (`IDENTITY`), o `rowversion`, coluna calculada, coluna com default de expressão (`DEFAULT (sysutcdatetime())`), coluna
com default constante (`DEFAULT (1)`) e as de auditoria (`UpdatedAt`, `ModifiedAt`, `UpdatedOn`). O `Update…Request` leva quase
tudo menos a chave — **inclusive** as de default constante, porque `IsActive` e `Status` mudam numa edição. Se a tabela tem
`rowversion`, o update e a resposta ganham `Version` (texto opaco), e o serviço gerado chama `ExpectVersion`: um `PUT` com a
versão velha vira **409 `CONCURRENCY_CONFLICT`**.

**O que o banco decide nas regras** (`…DatabaseRules.g.cs`): `NotEmpty()` para string obrigatória, `MaximumLength(n)` pelo
tamanho da coluna, `PrecisionScale(p, s)` para decimal quando o modelo traz precisão e escala, e `GreaterThan(0)` para FK
inteira. É uma regra de **forma**: o validador seu (`MovieValidators.cs`) faz `Include(...)` dessas regras e acrescenta as do
negócio (ano entre 1888 e 2100, URL absoluta…).

**O que o banco decide nos erros** (`…Constraints.g.cs`): para cada `UNIQUE`, um erro `<ENTIDADE>_<COLUNA>_ALREADY_EXISTS`
(**409**), com pré-checagem no serviço e o mapeamento da constraint para a corrida em que duas requisições passam juntas
pela pré-checagem; para cada FK em que a tabela é **dependente**, `<ENTIDADE>_<ALVO>_NOT_FOUND` (**422**, no `INSERT`/`UPDATE`);
para cada FK em que ela é **principal**, `<ENTIDADE>_HAS_<DEPENDENTES>` (**409**, no `DELETE`). `CHECK` não vira código
próprio: cai no genérico `CONSTRAINT_VIOLATED` (422) — se quiser um código seu, escreva um catálogo à mão (o
`LedgerConstraints` do PagWeb faz isso para os `CHECK` de saldo).

**`crudgen.json` — o que o banco não diz.** As chaves são **nomes de entidade** (como aparecem em
`Data/Generated/Entities`), não de tabela. Três decisões por entidade: `skip` (é feature escrita à mão), `excludeFromCreate` e
`excludeFromUpdate` (colunas que o serviço preenche). No nosso [`crudgen.json`](../crudgen.json), `AvailableCopies` sai do
`POST` e o estoque sai do `PUT`, porque estoque só muda por instrução atômica.

**O que o CrudGen pula — e por quê.** A saída mostra `skip` com o motivo:

| Motivo na saída | O que é | O que fazer |
|---|---|---|
| `skipped in crudgen.json (hand-written)` | você pediu | nada — é a sua feature |
| `hand-written feature (no Generated/ folder)` | existe `Business/<Feature>` **sem** `Generated/` | proteção do seu código; para gerar mesmo assim, mova a pasta (ou `--force` na ferramenta, que sobrescreve) |
| `view (read-only)` | é uma view | escreva um serviço de consulta à mão |
| `no single-column primary key` | chave composta | escreva à mão |
| `detail of <Mestre> (ON DELETE CASCADE)` | FK obrigatória com cascata: a linha pertence ao mestre (itens de um pedido) | o mestre é gerado **sem** os itens; mestre-detalhe se escreve à mão (veja o `PurchaseOrders` do TransFornecedores) |

**A lista de anônimos.** Cada fatia nova acrescenta `api/<rota>` à seção *testing phase* de
[`AnonymousEndpoints.txt`](../tests/FlixLocadora.Tests/Architecture/AnonymousEndpoints.txt) — o "interruptor" da
autenticação. O CrudGen **só acrescenta**: rota de tabela removida ou renomeada fica lá até você apagar.

> **No nosso caso:** veja `Business/Movies/` — os três `.g.cs` em `Generated/` e, fora dela, os arquivos seus com as regras:
> `MovieService.cs` (filtros e `SetCopiesAsync`), `MovieValidators.cs` (ano, nota, URL) e `MovieContracts.cs` (um arquivo
> escrito à mão, que estende `MovieListQuery` com `Genre` e `Available` por `partial`).

---

## 4. Passo a passo A — uma base nova

**Ensaiado:** um projeto novo do template com 4 tabelas de nomes em português (`Autores`, `Livros`, `Locacoes`,
`Fornecedores`). Resultado: **36 arquivos** gerados, build limpo e **58 testes verdes**; cerca de 50 s de `db-up` + scaffold +
CrudGen e 15 s de testes.

Há dois pontos de partida. Se você tem o template instalado (`dotnet new install <pasta>\_templates\InterviewApi`), comece
de um projeto novo — é o caminho mais limpo:

```powershell
dotnet new interview-api -n Biblioteca -o C:\coding\git\Biblioteca
cd C:\coding\git\Biblioteca
```

Se você clonou **este** repositório e quer reaproveitá-lo para outro banco, siga o [passo a passo D](#7-passo-a-passo-d--trocar-de-domínio-neste-repositório)
— ele limpa o domínio de locadora antes.

🔹 **1. Faça um commit antes de tudo.** O git é a rede de segurança: depois de cada ferramenta, `git status` e `git diff` mostram
exatamente o que ela mudou — e `git restore` desfaz.

🔹 **2. Traga o banco.** Dois jeitos:

- **Você escreve o DDL:** crie `database/schema/0001_<caso>.sql` (e, se quiser, `database/seed/0001_seed.sql` com dados
  **fictícios**).
- **O banco é entregue pronto:** aponte a connection string para ele — fora do repositório:
  ```powershell
  dotnet user-secrets set ConnectionStrings:AppDb "<connection string>" --project src/FlixLocadora.Api
  ```
  Mesmo assim, gere o DDL (SSMS → botão direito no banco → *Tasks → Generate Scripts…* → só schema) e salve como
  `database/schema/0001_<caso>.sql`: é **dele** que os testes criam o banco descartável de cada execução. E **não rode** o
  `db-up` contra o banco entregue — as tabelas já existem, o script `0001` falharia (a transação dele seria desfeita).

🔹 **3. Crie ou atualize o banco** (pule se o banco foi entregue):

```powershell
./scripts/db-up.ps1 -Seed
```

O que acontece: a ferramenta lê `ConnectionStrings:AppDb` (`appsettings.Development.json` → user-secrets → variável
`ConnectionStrings__AppDb`), cria o banco se ele não existir, cria o schema `deploy` e aplica, **em ordem de nome**, cada
script de `database/schema` que ainda não está no journal `deploy.SchemaVersions` — cada um na sua transação. Com `-Seed`,
faz o mesmo com `database/seed`. A connection string nunca é impressa.

🔹 **4. Gere o modelo:**

```powershell
./scripts/scaffold.ps1
```

O que acontece, nos cinco passos do script: (1) compila o projeto — o `dotnet ef` lê a configuração de dentro do build; (2)
move o conteúdo de `Data/Generated` para um backup temporário, para que tabela apagada vire arquivo apagado; (3) roda o
scaffold com os templates de [`CodeTemplates/EFCore`](../src/FlixLocadora.Api/CodeTemplates/EFCore) — cada entidade é uma
`partial class` com o marcador `IEntity`, cada configuração tem `ToTable` explícito, o default constante do banco vira
inicializador C#; (4) compila de novo e, se o código **fora** de `Data/Generated` quebrou, lista os erros `arquivo:linha`; (5)
com `-Test`, roda os testes `ModelVsDatabase`. Se o scaffold em si falhar, o backup volta.

**Confira agora os nomes** em `src/FlixLocadora.Api/Data/Generated/Entities`. É o momento mais barato para decidir sobre
nomes estranhos (no ensaio: `Autore.cs`, `Fornecedore.cs`, `Locaco.cs` — veja a [seção 8](#8-nomes-em-português--o-que-sai-hoje-e-como-lidar)).

🔹 **5. Decida o que o banco não diz** — edite o [`crudgen.json`](../crudgen.json) com os nomes de **entidade** que você acabou
de ver:

```jsonc
{
  "entities": {
    "Locacao": { "skip": true },                               // operação de negócio: escrita à mão
    "Livro": { "excludeFromCreate": [ "Disponiveis" ] }        // o serviço preenche
  }
}
```

🔹 **6. Gere as fatias** — veja antes, sem gravar nada, e depois de verdade:

```powershell
./scripts/crudgen.ps1 -DryRun
./scripts/crudgen.ps1
```

O que acontece: o CrudGen lê o banco direto — **não depende de a API compilar**, por isso funciona no meio de uma troca de
domínio — e, para cada tabela, escreve os três `.g.cs`, cria os arquivos seus que faltam, acrescenta a rota à lista de
anônimos e regenera `docs/modelo-de-dados.md`. Depois compila a solução inteira. Trecho da saída do ensaio:

```
created     src\Biblioteca.Api\Business\Livros\Generated\LivroContracts.g.cs
created     src\Biblioteca.Api\Business\Livros\Generated\LivroDatabaseRules.g.cs
created     src\Biblioteca.Api\Business\Livros\Generated\LivroConstraints.g.cs
created     src\Biblioteca.Api\Business\Livros\LivroValidators.cs
created     src\Biblioteca.Api\Business\Livros\LivroConverter.cs
created     src\Biblioteca.Api\Business\Livros\LivroService.cs
created     src\Biblioteca.Api\Controllers\LivrosController.cs
created     tests\Biblioteca.Tests\Api\LivrosApiTests.cs
anonymous   api/livros (testing phase — remove at the auth go-live)
...
36 change(s).
Build succeeded.
```

Uma tabela sem `UNIQUE` nem FK (no ensaio, `Fornecedores`) não ganha o `…Constraints.g.cs` — o gerador não tem o que dizer.

🔹 **7. Teste:**

```powershell
dotnet test
```

O que acontece: um banco descartável é criado a partir de `database/schema` (o mesmo DbUp), a API sobe em memória contra
ele, e rodam os testes de fumaça de cada fatia (lista → 200, id inexistente → 404), os `ModelVsDatabase` (toda entidade
aponta para tabela e coluna que existem; toda tabela tem entidade), os de arquitetura (o DI resolve tudo, endpoints anônimos
batem com a lista, ninguém editou `Generated`) e os de infraestrutura. No fim, o banco é apagado.

🔹 **8. Rode e veja:**

```powershell
dotnet run --project src/FlixLocadora.Api --launch-profile http
```

Abra `http://localhost:5406/scalar`: cada fatia aparece com os contratos gerados, e os erros 400/404/500 já documentados com o
envelope.

🔹 **9. Escreva as regras do caso — onde operar:**

| Você precisa de… | Onde | Exemplo neste projeto |
|---|---|---|
| filtro ou busca na listagem | `<X>Service.Filter` (traduzido para SQL) | `MovieService.Filter` (gênero, disponível) |
| campos novos na query string | `partial class <X>ListQuery` num arquivo seu + regra no `<X>ListQueryValidator` | `Business/Movies/MovieContracts.cs` |
| ordem da listagem | `<X>Service.Order` | `MovieService.Order` (título, depois id) |
| valor que o servidor define ao criar | `OnCreating` (e `OnUpdating` ao editar) | `MovieService.OnCreating` (estoque disponível = total) |
| regra que consulta o banco | `CheckCreateAsync` / `CheckUpdateAsync` / `CheckDeleteAsync` | `MovieService.CheckDeleteAsync` (filme com histórico → 409) |
| regra de formato (CPF, faixa, URL) | `<X>Validators.cs`, depois do `Include(...DatabaseRules)` | `CustomerValidators.cs` (CPF com dígito verificador) |
| normalizar um campo ao gravar | `<X>Converter.cs` com `[MapProperty(..., Use = nameof(...))]` | `CustomerConverter.cs` (CPF e telefone só com dígitos) |
| endpoint além do CRUD | controller (arquivo seu) + método no serviço | `PATCH /api/movies/{id}/copies` |
| código de erro tipado para uma constraint que nenhuma fatia gerada cobre (um `CHECK`, as FKs de uma feature escrita à mão) | um `ConstraintErrorCatalog` escrito à mão | `RentalConstraints` em `Business/Rentals/RentalContracts.cs` (as FKs da locação no `INSERT`) |
| operação de negócio que não é cadastro | `skip` no `crudgen.json` + feature escrita à mão | `Business/Rentals/` |

🔹 **10. Commit.** E, ao longo do caso, cada mudança de banco é um **script novo** (`0002_…`, `0003_…`) seguido de `db-up` →
`scaffold` → `crudgen` → `test`.

---

## 5. Passo a passo B — renomear tabelas num projeto que já existe

Antes dos comandos, o que um rename atinge. O nome da tabela é a origem de quase todos os nomes do C# (seção 3); trocar a
tabela troca, em cascata:

| O que muda | Automático? |
|---|---|
| entidade, configuração e navegações (`Data/Generated`) | ✅ scaffold |
| `.g.cs` da fatia: contratos, regras, catálogo de constraints, **códigos de erro** gerados (`MOVIE_HAS_RENTALS` → `FILM_HAS_LOANS`) | ✅ CrudGen |
| pasta da feature, namespace, controller, **rota** (`api/movies` → `api/films`) | ✅ para fatias geradas (uma fatia **nova**); ❌ para o seu código |
| arquivos seus que citam o nome antigo (serviço, conversor, repositório, testes) | ❌ você — o compilador lista |
| chaves do `crudgen.json` | ❌ você — **antes** de rodar o CrudGen |
| rotas antigas no `AnonymousEndpoints.txt` | ❌ você — o CrudGen só acrescenta |
| nomes de constraint (`PK_Movie`, `FK_Rental_Movie`…) | ficam como estão, a menos que você também os renomeie no script |

> **Rota e código de erro são contrato público.** Clientes podem depender deles. Num rename, decida se a rota acompanha
> (`[Route("api/films")]`) ou se você mantém a antiga no controller por compatibilidade.

### B.1 — Projeto em que as fatias são só geradas (ensaiado)

Ensaio: as 4 tabelas do passo A renomeadas para o singular, e uma delas para outro nome (`Fornecedores` → `Editora`).

🔹 **1. Commit.**

🔹 **2. Um script novo** — nunca edite o `0001` (o DbUp registra por nome e não reaplica script editado):

```sql
-- database/schema/0002_rename_tables.sql
EXEC sp_rename N'dbo.Autores', N'Autor';
EXEC sp_rename N'dbo.Livros', N'Livro';
EXEC sp_rename N'dbo.Locacoes', N'Locacao';
EXEC sp_rename N'dbo.Fornecedores', N'Editora';
-- opcional: renomear também as constraints, para os nomes contarem a verdade
-- EXEC sp_rename N'dbo.PK_Autores', N'PK_Autor', N'OBJECT';
```

📖 O `sp_rename` preserva dados, chaves e constraints; o terceiro parâmetro (`OBJECT`, `COLUMN`, `INDEX`) diz o que se renomeia
([docs](https://learn.microsoft.com/sql/relational-databases/system-stored-procedures/sp-rename-transact-sql)). A própria
documentação avisa que renomear quebra scripts e procedures que citam o nome antigo — aqui, quem cita é o C#, e o compilador
avisa.

🔹 **3.** `./scripts/db-up.ps1` — aplica o `0002`.

🔹 **4.** `./scripts/scaffold.ps1` — **vai falhar no passo 4, e isso é esperado**:

```
error CS0246: The type or namespace name 'Autore' could not be found   (Business\Autores\AutoreConverter.cs)
The new model broke code outside Data/Generated: see the errors above.
```

O `Data/Generated` **já está atualizado** (`Autor.cs`, `Editora.cs`, `Livro.cs`, `Locacao.cs`); quem quebrou são os arquivos
seus das fatias antigas. Não desfaça nada: siga.

🔹 **5. Atualize as chaves do `crudgen.json`** para os nomes novos (no ensaio não havia nenhuma; no B.2 isso é crítico).

🔹 **6. Gere as fatias novas e remova as velhas:**

```powershell
./scripts/crudgen.ps1 -PruneOwned
```

O que acontece: as fatias das entidades que sumiram (`Autore`, `Fornecedore`, `Locaco`) são apagadas — os `.g.cs` **e** os
arquivos seus (validadores, conversor, serviço, controller, testes) — e as novas (`Autor`, `Editora`, `Locacao`) são criadas.
Com `-Prune` (sem `Owned`), só os `.g.cs` saem, e os arquivos seus aparecem como `orphan` para você revisar — use esse quando
as fatias velhas tiverem regras que você queira mover.

🔹 **7. Corrija as fatias que sobreviveram.** No ensaio, `Livro` não mudou de nome, mas os vizinhos mudaram, e os arquivos
seus dela citavam os nomes velhos — **10 erros**, todos em dois arquivos:

```
LivroConverter.cs: error CS0117: 'Livro' does not contain a definition for 'Locacos'
LivroConverter.cs: error RMG012 / RMG004 / RMG020 (Mapperly: a navegação 'Locacaos' não tem par)
LivroService.cs:   error CS0246: The type or namespace name 'Locaco' could not be found
```

Dois jeitos: **à mão** (troque `Locacos` por `Locacaos` e `Locaco` por `Locacao`), ou — se o arquivo ainda não tem regra sua —
**apague-o e rode o CrudGen**, que recria o que falta. No ensaio:

```powershell
Remove-Item src/Biblioteca.Api/Business/Livros/LivroConverter.cs, src/Biblioteca.Api/Business/Livros/LivroService.cs
./scripts/crudgen.ps1        # created LivroConverter.cs, created LivroService.cs — 2 change(s), build ok
```

🔹 **8. Limpe as rotas antigas** de `tests/<Projeto>.Tests/Architecture/AnonymousEndpoints.txt` (no ensaio: `api/autores`,
`api/fornecedores`, `api/locacos`).

🔹 **9.** `dotnet test` — no ensaio, **58 verdes** de novo. 🔹 **10. Commit.**

### B.2 — Projeto com código escrito à mão (o caso deste repositório)

Ensaio num clone do FlixLocadora: `Movie` → `Film`, `Customer` → `Client`, `Rental` → `Loan`, contra um banco descartável.
O ensaio foi até o levantamento do que quebra e do comportamento do CrudGen; a refatoração em si é o roteiro abaixo.

**O que o compilador apontou depois do scaffold** — 7 arquivos, todos de código seu:

| Arquivo | Por quê |
|---|---|
| `Business/Movies/MovieService.cs`, `MovieConverter.cs` | `CrudService<Movie, …>` e o mapeamento citam `Movie` |
| `Business/Customers/CustomerService.cs`, `CustomerConverter.cs` | idem com `Customer` |
| `Business/Rentals/RentalService.cs`, `RentalPricing.cs` | a feature escrita à mão usa `Rental`, `Movie`, `Customer` |
| `Data/Repositories/MovieRepository.cs` | o repositório específico é `Repository<Movie>` |

Os testes (`RentalsTests` usa `db.Set<Rental>()`) entram na lista assim que a API voltar a compilar.

**A armadilha do `crudgen.json`.** Com as chaves antigas (`"Movie"`, `"Rental"`), o `-DryRun` mostrou que o CrudGen criaria
uma fatia **CRUD completa para `Loan`** — por cima da sua feature de locações — e geraria `Film` sem as exclusões de estoque.
Com as chaves renomeadas (`"Film"`, `"Loan"`), a saída virou `skip Loan: skipped in crudgen.json (hand-written)`. **Atualize o
`crudgen.json` antes de rodar o CrudGen.**

**O roteiro recomendado:**

1. commit; script `0002` com `sp_rename`; `db-up`; `scaffold` (vai listar os 7 arquivos);
2. `crudgen.json`: troque as chaves (`Movie` → `Film`, `Rental` → `Loan`);
3. **renomeie com o IDE**, não com busca e troca de texto: `F2` no VS Code, `Ctrl+R, Ctrl+R` no Visual Studio, `Shift+F6` no
   Rider. O rename semântico troca o tipo e as referências **sem** tocar em `MovieId` — que é nome de **coluna** e não mudou
   (um `replace` de "Movie" por "Film" quebraria o mapeamento);
4. **apague** `Business/Movies/Generated/` (a pasta é toda do gerador — ele a recria) e **mova** os arquivos seus para a pasta
   do plural novo, já com os nomes novos: `Business/Movies/*.cs` → `Business/Films/` (`MovieService.cs` → `FilmService.cs`…),
   `Controllers/MoviesController.cs` → `FilmsController.cs`, `tests/FlixLocadora.Tests/Api/MoviesApiTests.cs` →
   `FilmsApiTests.cs`; o mesmo para `Customers` → `Clients`;
5. `./scripts/crudgen.ps1` — cria os `.g.cs` novos em `Business/Films/Generated/` e **não toca** nos seus, que já existem;
6. compile e corrija o que restar (navegações renomeadas, `Rental.Movie` → `Loan.Film`); decida as rotas (contrato público);
7. `AnonymousEndpoints.txt`, o arquivo `.http`, o README; `dotnet test`; commit.

> ⚠️ **Não rode `-PruneOwned` depois de mover pastas.** O prune deduz o controller e o teste de uma fatia pelo **nome da pasta**
> ([`tools/CrudGen/Program.cs`](../tools/CrudGen/Program.cs), método `Prune`): ao achar um `MovieContracts.g.cs` esquecido dentro
> de `Business/Films/Generated`, ele trataria `FilmsController.cs` e `FilmsApiTests.cs` — os seus, já renomeados — como restos
> da fatia velha e os apagaria. Apagar a `Generated` velha no passo 4 evita isso. (Dedução do código, não executada no ensaio.)

---

## 6. Passo a passo C — reiniciar o mapeamento num schema novo

Ensaio: as 4 tabelas movidas de `dbo` para o schema `acervo`. Resultado: `ToTable("Autor", "acervo")` nas configurações,
**11** `.g.cs` atualizados (só o nome da tabela nos comentários) e **58 testes verdes**.

🔹 **1. Commit.** 🔹 **2. Um script novo:**

```sql
-- database/schema/0003_move_to_acervo.sql
CREATE SCHEMA acervo;
GO
ALTER SCHEMA acervo TRANSFER dbo.Autor;
ALTER SCHEMA acervo TRANSFER dbo.Livro;
ALTER SCHEMA acervo TRANSFER dbo.Locacao;
ALTER SCHEMA acervo TRANSFER dbo.Editora;
```

📖 `ALTER SCHEMA … TRANSFER` move a tabela com dados, índices e constraints; as permissões dadas direto no objeto são
descartadas ([docs](https://learn.microsoft.com/sql/t-sql/statements/alter-schema-transact-sql)). O `GO` separa lotes — o DbUp
entende.

🔹 **3.** `./scripts/db-up.ps1`

🔹 **4. Scaffold e CrudGen com o schema — sempre os dois:**

```powershell
./scripts/scaffold.ps1 -Schemas acervo
./scripts/crudgen.ps1 -Schemas acervo
```

O que acontece: o contexto continua com `HasDefaultSchema("dbo")` (é o schema padrão do usuário da conexão) e cada
configuração passa a dizer o schema, porque ele difere do padrão: `builder.ToTable("Autor", "acervo")`. O Identity (`auth`) e
o journal (`deploy`) nunca entram no scaffold. Vários schemas: `-Schemas dbo,acervo` — evite nomes de tabela iguais em
schemas diferentes, porque as entidades colidiriam.

🔹 **5. Torne permanente:** troque o padrão `$Schemas = @('dbo')` no `param(...)` de **dois** arquivos —
[`scripts/scaffold.ps1`](../scripts/scaffold.ps1) e [`scripts/crudgen.ps1`](../scripts/crudgen.ps1) — para `@('acervo')`. Se
mudar só um, scaffold e CrudGen passam a ler bancos diferentes.

🔹 **6.** `dotnet test` (os `ModelVsDatabase` usam os schemas do próprio modelo); **commit.**

### A armadilha — e como sair dela (ensaiado)

**Esquecer o `-Schemas`** depois de mover as tabelas faz o scaffold ler o `dbo`, agora vazio: o modelo sai **sem nenhuma
entidade**, o build quebra — e o scaffold seguinte, mesmo com `-Schemas acervo`, se **recusa** a rodar
(`The project does not build: fix it before scaffolding.`), porque o passo 1 dele exige um projeto que compile. A saída:

```powershell
git restore src/FlixLocadora.Api/Data/Generated      # volta o modelo do último commit (por isso o commit do passo 1)
./scripts/scaffold.ps1 -Schemas acervo
```

**E não rode o CrudGen com `-Prune`/`-PruneOwned` nesse estado.** Pela lógica do código ([`tools/CrudGen/Program.cs`](../tools/CrudGen/Program.cs)):
com o schema errado ele não encontra entidade nenhuma, e o prune apagaria **todas** as fatias. (Dedução do código, não
executada no ensaio.)

---

## 7. Passo a passo D — trocar de domínio neste repositório

Para reaproveitar **este** repositório com um banco completamente diferente (o template fica fora do GitHub). Roteiro montado
a partir do que os ensaios A e B mostraram sobre o alcance de cada comando:

1. **Branch e commit** (`git switch -c novo-caso`).
2. **Banco novo, DDL novo:** troque o conteúdo de `database/schema/` e `database/seed/` — aqui pode apagar os scripts antigos,
   porque é **outro banco** (o journal é por banco); em `src/FlixLocadora.Api/appsettings.Development.json`, troque
   `Database=FlixLocadora` pelo nome novo.
3. `./scripts/db-up.ps1 -Seed` e `./scripts/scaffold.ps1` (o build vai acusar o domínio antigo — esperado).
4. **`crudgen.json`:** apague as decisões antigas (`Movie`, `Rental`) e escreva as novas.
5. `./scripts/crudgen.ps1 -PruneOwned` — remove as fatias geradas da locadora (`Movies`, `Customers`), com os arquivos seus.
   **Atenção ao alcance:** ele remove os três `.g.cs` e os cinco arquivos seus padrão de cada fatia; **outros** arquivos que
   você criou na pasta da feature ficam (aqui: `Business/Movies/MovieContracts.cs`).
6. **Apague à mão o que é escrito à mão** — o CrudGen não toca nisso:
   - `Business/Rentals/` e `Controllers/RentalsController.cs`;
   - `Business/Movies/MovieContracts.cs`, `Data/Repositories/MovieRepository.cs`;
   - `Infra/DomainOptions.cs` (as opções de locação) e a seção `"Rental"` do `appsettings.json`; a chamada
     `.AddDomainOptions()` em `Program.cs` se não houver mais opções;
   - testes do domínio: `tests/FlixLocadora.Tests/Api/RentalsTests.cs`, `Business/RentalPricingTests.cs`,
     `Support/ShopClient.cs` e os construtores `NewMovie`/`NewCustomer` de `Support/TestData.cs`.
7. **`AnonymousEndpoints.txt`:** tire `api/movies`, `api/customers`, `api/rentals`.
8. **O resto do texto:** `src/FlixLocadora.Api/FlixLocadora.Api.http`, este README, `docs/PROGRESS.md`.
9. `dotnet build` → `dotnet test` → commit.

O nome do projeto (`FlixLocadora.Api`, namespaces) continua o mesmo; renomear a solução é outra refatoração — por isso o
template existe.

---

## 8. Nomes em português — o que sai hoje e como lidar

**Ensaiado.** Tabelas no plural em português saem assim:

| Tabela | Entidade | Feature e rota |
|---|---|---|
| `Autores` | `Autore` | `Autores` · `api/autores` |
| `Fornecedores` | `Fornecedore` | `Fornecedores` · `api/fornecedores` |
| `Livros` | `Livro` ✅ | `Livros` · `api/livros` |
| `Locacoes` | `Locaco` | `Locacos` · `api/locacos` |

E no singular: `Autor` → `Autor` ✅, mas a feature vira `Autors` (`api/autors`); `Locacao` → `Locacao` ✅ e `Locacaos`.

**Por quê.** São duas regras do **inglês** em série. Primeiro, o scaffold (e o CrudGen, que lê o mesmo modelo) passa o nome
da tabela pelo singularizador do EF: `-oes` é tratado como em `heroes` → `hero` (`Locacoes` → `Locaco`), e nos demais casos
ele só tira o `-s` final (`Autores` → `Autore`). Depois, o CrudGen pluraliza a entidade com as regras de
[`Naming.Plural`](../tools/CrudGen/Naming.cs) (`y` → `ies`, `s/x/z/ch/sh` → `+es`, senão `+s`): `Autor` → `Autors`.

**O que fazer hoje:**

| Opção | Efeito | Custo |
|---|---|---|
| aceitar os nomes | funciona, testes verdes | os nomes estranhos aparecem na **API** (rota `api/locacos`, código `LOCACO_…`) |
| corrigir só a rota | no controller (arquivo seu): `[Route("api/locacoes")]`, e a linha no `AnonymousEndpoints.txt` | as classes continuam `Locaco`/`Locacos` |
| renomear as tabelas (se o banco é seu) | passo a passo B | é refatoração |

**A correção durável** — um pluralizador PT-BR registrado no scaffold (`IPluralizer` via `IDesignTimeServices`) e a mesma
regra no CrudGen (`ModelReader.Read` e `Naming.Plural`) — **ainda não existe** neste projeto. Uma regra para quem for
mexer nisso: **scaffold e CrudGen precisam concordar** — os dois usam o pluralizador ligado (`NoPluralize = false`); se um
mudar (por exemplo, `--no-pluralize` no scaffold) e o outro não, o CrudGen gera código para entidades que não existem.

---

## 9. Onde operar — resumo

| Situação | Onde | O quê |
|---|---|---|
| mudar o banco (coluna, tabela, índice, constraint) | `database/schema/000N_<o-que>.sql` | script **novo**; nunca editar um aplicado |
| dado fictício de desenvolvimento | `database/seed/` | idem; só com `-Seed` |
| apontar para outro banco | user-secrets `ConnectionStrings:AppDb`, ou variável `ConnectionStrings__AppDb` | nunca num arquivo versionado se tiver senha |
| outro schema | `-Schemas` nos **dois** scripts, ou o padrão no `param(...)` deles | seção 6 |
| decidir pular / excluir coluna | `crudgen.json` (chave = nome da **entidade**) | **antes** do CrudGen |
| regra de negócio de uma fatia | arquivos seus: `<X>Service.cs`, `<X>Validators.cs`, `<X>Converter.cs`, controller | tabela do passo 9 da seção 4 |
| membro a mais numa entidade gerada | `Data/Partials/<Entidade>.cs` (`partial class`) | nunca em `Data/Generated` |
| configuração a mais do EF | `Data/Partials/AppDbContext.cs` (`OnModelCreatingPartial`, `ConfigureConventions`) | idem |
| regenerar um arquivo seu sem personalização | apague-o e rode `crudgen.ps1` | ele recria o que falta |
| fatias de tabelas que sumiram | `crudgen.ps1 -Prune` (só gerados) ou `-PruneOwned` (com os seus) | revise o `git status` depois |
| rota anônima velha | `tests/FlixLocadora.Tests/Architecture/AnonymousEndpoints.txt` | apague a linha |
| como as entidades são geradas | `src/FlixLocadora.Api/CodeTemplates/EFCore/*.t4` | mudança aqui vale para todas — rode o scaffold e confira o diff |
| como as fatias são geradas | `tools/CrudGen/Generators.cs` (e `Naming.cs`) | idem, com o CrudGen |

---

## 10. Problemas comuns

| Sintoma | Causa | O que fazer |
|---|---|---|
| `The project does not build: fix it before scaffolding.` | o build já estava quebrado (por exemplo, um scaffold anterior com o schema errado) | `git restore src/FlixLocadora.Api/Data/Generated` e rode o scaffold certo |
| `The new model broke code outside Data/Generated` | esperado depois de renomear ou remover tabela | siga a lista `arquivo:linha`; o CrudGen roda mesmo assim |
| erros `RMG012` / `RMG020` / `RMG004` (Mapperly) | coluna ou navegação nova/renomeada sem par no conversor | ajuste o conversor (`MapperIgnore…`, `MapProperty`) ou apague-o (sem personalização) e rode o CrudGen |
| `skip X: hand-written feature (no Generated/ folder)` | existe `Business/<Feature>` sem `Generated/` | é a proteção do seu código (seção 3) |
| a fatia nova não aparece no Scalar | a API não foi reiniciada, ou a pasta tinha outro nome | reinicie; confira a saída do CrudGen |
| `ModelVsDatabase`: `… does not exist` | modelo e banco divergem | rode o scaffold; confira para qual banco aponta o `AppDb` |
| editei um script já aplicado e nada mudou | o DbUp registra o **nome** do script e não o reexecuta | desfaça a edição e escreva um script novo |
| os testes não acham as tabelas de um banco entregue | o banco de teste nasce de `database/schema` | gere o DDL do banco entregue em `0001_<caso>.sql` |
| `api/<rota-velha>` continua na lista de anônimos | o CrudGen só acrescenta | apague a linha |

---

## 11. Roteiro de investigação e próximo passo

Para explorar sozinho, em ordem:

1. `./scripts/crudgen.ps1 -DryRun` — tudo o que ele faria, sem gravar; rode antes de qualquer execução de verdade.
2. `git status` e `git diff --stat` depois de cada ferramenta — o que exatamente mudou.
3. Abra um `.g.cs` e um arquivo seu da mesma fatia e compare os cabeçalhos (`// <auto-generated>` × `// Created by CrudGen`).
4. `docs/modelo-de-dados.md` — o diagrama Mermaid que o CrudGen gera (o GitHub e o VS Code o desenham).
5. No SSMS ou na extensão *SQL Server (mssql)* do VS Code: `SELECT * FROM deploy.SchemaVersions ORDER BY Applied;` — o
   histórico do DbUp.
6. `dotnet test --filter Category=ModelVsDatabase` — e, contra um banco entregue:
   `$env:MODEL_DB_CONNECTION = "<connection string>"; dotnet test --filter Category=ModelVsDatabase; Remove-Item Env:MODEL_DB_CONNECTION`.
7. Ponha um breakpoint em `ModelReader.Read` ([`tools/CrudGen/ModelReader.cs`](../tools/CrudGen/ModelReader.cs)) e rode o
   CrudGen pelo depurador: o `IModel` que aparece é o mesmo que os templates T4 recebem no scaffold.

**Próximo passo:** depois de gerar as fatias de um banco seu e conferir o diff, escreva a primeira regra num gancho —
`CheckCreateAsync` com um teste de integração que espera o 409 ou o 422. `MovieService.cs` e `MoviesApiTests.cs` são o
exemplo deste projeto; a parte "Como a API funciona" do [README](../README.md) explica o caminho que essa requisição percorre.
