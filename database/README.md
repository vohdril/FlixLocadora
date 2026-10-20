# database/

The **source of truth of the domain** (database-first). Applied in order by `tools/FlixLocadora.Database` (DbUp):

| Folder | When | Rule |
|---|---|---|
| `schema/` | always (`scripts/db-up.ps1`) and in every test run | `0001_<what>.sql`, `0002_<what>.sql`… — an applied script is never edited: changes are new scripts |
| `seed/` | only with `scripts/db-up.ps1 -Seed` | fictitious data only — never real people, companies or documents |

The journal lives in `deploy.SchemaVersions`; Identity lives in `auth.*` (migrations of AuthDbContext). The scaffold
reads only `dbo` (or the schemas you pass to `scripts/scaffold.ps1 -Schemas`).

Got a database from the case study instead of scripts? Point `ConnectionStrings:AppDb` at it and go straight to
`scripts/scaffold.ps1` — `MODEL_DB_CONNECTION` makes the ModelVsDatabase tests check it too.
