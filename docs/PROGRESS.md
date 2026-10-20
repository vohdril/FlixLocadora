# Progresso — FlixLocadora

Criado a partir do template `interview-api` (esqueleto do TransFornecedores, .NET 10). Planejamento: `C:\coding\planejamento\07-flixlocadora.md`.

| Etapa | Estado | Notas |
|---|---|---|
| Banco do caso em `database/schema` | ✅ | `0001_movies_customers_rentals.sql` (CHECKs de estoque e datas, índices filtrados de locações abertas) + seed fictício |
| `db-up` → `scaffold` → `crudgen` | ✅ | Movie e Customer pelo CrudGen; `crudgen.json` tira o estoque do POST/PUT e pula Rental |
| Regras de negócio | ✅ | filmes (estoque atômico, filtros), clientes (CPF, normalização), locações escritas à mão |
| Testes dos cenários | ✅ | 90 verdes; corridas conferidas por mutação (sem `UPDLOCK`, falham) |
| Go-live da autenticação | ⏳ | roteiro no README (3 passos) — fica para quando houver front |

## Decisões tomadas na construção (além do planejamento)

| Tema | Planejado | Construído | Por quê |
|---|---|---|---|
| Ativar/inativar filme | `PATCH /api/movies/{id}/status` | `IsActive` no `PUT` gerado pelo CrudGen | o CrudGen já coloca a coluna no contrato de update; um endpoint a menos |
| Estoque | ajuste pelo `PUT` | `PATCH /api/movies/{id}/copies` + `crudgen.json` | estoque é regra (não pode ficar abaixo das cópias alugadas), não campo de formulário |
| Limite por cliente | checagem no serviço | checagem com a linha do cliente travada (`UPDLOCK`) | "contar, conferir, inserir" também é corrida |
| Devolver/prorrogar/cancelar | — | linha da locação travada (`FindForUpdateAsync`) | clique duplo devolvia a cópia duas vezes |
| Prorrogação | até `MaxDays` | + `RENTAL_OVERDUE` e `RENTAL_DUE_DATE_NOT_LATER` | prorrogar uma locação atrasada apagaria a multa |
| Cancelamento | locação aberta | + janela `Rental:CancelWindowMinutes` (30) | cancelar dias depois apagaria o valor devido |
| Datas | — | UTC com `Z`, conversão no EF | `datetime2` volta sem tipo; conversor JSON apagava o `type` do OpenAPI |
| `FindForUpdateAsync` | — | no repositório genérico | trava pessimista de uma linha, reaproveitável; candidata ao template |
