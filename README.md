# route-gen — Serviço de roteirização e embarque em tempo real

Microsserviço .NET responsável por:

- **gerar a melhor rota** para um veículo a partir dos pontos de embarque dos alunos
  (agrupando pontos próximos e otimizando a ordem até o destino);
- **acompanhamento em tempo real** (SignalR/WebSocket) para aluno e motorista;
- **confirmação de embarque rápida** por geolocalização, com propagação assíncrona
  da presença ao serviço de attendance.

O serviço é um web app HTTP que escuta em `:8080` e expõe tudo sob `/api/rotas`
(é o prefixo roteado pelo API Gateway para a roteirização).

## Endpoints

| Método | Rota | Papel | Descrição |
|--------|------|-------|-----------|
| `PUT`  | `/api/rotas/ponto-embarque` | aluno (próprio) / admin | Cadastra/atualiza o ponto de embarque (lat/long) de um aluno |
| `GET`  | `/api/rotas/ponto-embarque/{alunoId}` | aluno (próprio) / admin | Consulta o ponto de embarque |
| `POST` | `/api/rotas/gerar` | admin / motorista | Gera e persiste a melhor rota para um veículo |
| `GET`  | `/api/rotas/{id}` | todos (aluno vê só a própria parada) | Consulta uma rota persistida |
| `GET`  | `/api/rotas/motorista/atual?veiculoId=` | admin / motorista | Rota corrente do dia de um veículo |
| `POST` | `/api/rotas/{rotaId}/posicao` | admin / motorista | Telemetria: publica a posição atual da van |
| `POST` | `/api/rotas/{rotaId}/paradas/{alunoId}/confirmar` | aluno (próprio) / admin | Confirma embarque por geolocalização |
| `WS`   | `/api/rotas/hub` | aluno / motorista | Hub SignalR de tempo real |
| `GET`  | `/health`, `/api/rotas/health` | — | Health check |

### Eventos de tempo real (SignalR)

Cliente assina via `SubscreverRota(rotaId)` (motorista/admin) ou
`SubscreverAluno(rotaId)` (aluno). Eventos publicados:

- `RotaAtualizada` — rota gerada/atualizada;
- `PosicaoVan` — `{ rotaId, latitude, longitude }`;
- `EmbarqueConfirmado` — parada confirmada.

A autorização do hub e dos endpoints usa os headers que o gateway injeta após
validar o JWT: `X-User-Role` (`ROLE_ADMIN`/`ROLE_MOTORISTA`/`ROLE_ALUNO`) e
`X-Profile-Id`. O serviço **não** revalida o token (mesmo padrão dos demais
microsserviços downstream).

## Geração da "melhor rota"

1. Carrega os pontos de embarque (e, se configurado, filtra alunos via serviço de
   cadastro por `rotaTransporte`/`cursoId`).
2. **Clustering** por raio configurável (`Routing:ClusterRadiusMeters`, padrão 300 m):
   pontos próximos viram uma única parada (centroide).
3. **Otimização** da ordem com distância **Haversine**: vizinho mais próximo +
   melhoria local **2-opt**, partindo da origem do veículo até o destino.
4. Persiste a rota e as paradas para leitura imediata e emite `RotaAtualizada`.

## Confirmação de embarque rápida

`POST /api/rotas/{rotaId}/paradas/{alunoId}/confirmar` com `{ latitude, longitude }`:

- valida a distância Haversine até a parada contra `Routing:ConfirmationRadiusMeters`
  (padrão 100 m) — fora do raio retorna `422`;
- grava a confirmação (idempotente) e **emite `EmbarqueConfirmado` imediatamente**;
- enfileira a propagação da presença ao attendance
  (`POST /api/v1/presencas/aluno/{alunoId}/curso/{cursoId}/confirmar-hoje?status=PRESENTE`),
  processada por um worker em background — **fora do caminho crítico**.

## Configuração

| Chave | Descrição |
|-------|-----------|
| `ConnectionStrings:DefaultConnection` | PostgreSQL (Cloud SQL em produção, `route-gen-db` local) |
| `ConnectionStrings:Redis` | Backplane do SignalR (opcional; necessário com múltiplas réplicas) |
| `Routing:ClusterRadiusMeters` | Raio de agrupamento de pontos (m) |
| `Routing:ConfirmationRadiusMeters` | Raio máximo para confirmar embarque (m) |
| `Services:RegisterBaseUrl` | URL base do serviço de cadastro (opcional) |
| `Services:AttendanceBaseUrl` | URL base do serviço de presença (opcional) |

As credenciais **não** são versionadas: em produção use os secrets do Container App
(`route_gen_db_url`) e o backplane Redis via connection string.

As migrações EF Core são aplicadas automaticamente no startup (idempotente).

## Execução local

```bash
docker compose up -d
# API em http://localhost:8085  (container escuta em :8080)
```

## Google Cloud SQL (produção)

A instância é PostgreSQL com IP público. Para a Azure Container App alcançá-la:

1. Definir `route_gen_db_url` (no `infra/tofu/terraform.tfvars`) como connection
   string ADO.NET:
   `Host=34.136.228.175;Port=5432;Database=route_gen_db;Username=...;******;SSL Mode=Require;Trust Server Certificate=true`.
2. Adicionar os IPs de saída da Container App às *Authorized networks* do Cloud SQL.
3. Manter o SSL obrigatório (no futuro, preferir Cloud SQL Auth Proxy / IP privado).

> Local/dev continua usando `route-gen-db` (a rede `backend` do compose de infra é
> `internal: true`, sem acesso à internet/Cloud SQL).

## Mudanças necessárias em repositórios vizinhos

Este serviço foi reposicionado de gRPC/HTTPS:5001 para HTTP `:8080`. Para a
integração funcionar ponta a ponta, os repositórios abaixo precisam de ajustes:

- **infra (`infra/tofu/main.tf`, app `route-generator`)**: `target_port = 8080`,
  `transport = "http"` (com WebSocket habilitado), `min_replicas = 1` (evita cold
  start e sustenta as conexões de tempo real); manter `route_gen_db_url` apontando
  para o Cloud SQL.
- **api-gateway (`application.yml`)**: definir `ROTEIRIZACAO_SERVICE_URL=http://route-generator`
  (porta correta) e garantir o upgrade de WebSocket para `/api/rotas/hub`,
  repassando `X-User-Role`/`X-Profile-Id`.
- **frontend**: cliente SignalR consumindo `/api/rotas/hub`; telas de motorista
  (rota + posição da van em tempo real) e aluno (ETA + botão "Confirmar embarque");
  migrar o ponto de embarque do `localStorage` para `PUT /api/rotas/ponto-embarque`.
