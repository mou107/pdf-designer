# pdf-designer — Microservice de modèles PDF Stimulsoft pour Axiobat

Microservice .NET 8 (pattern `customExport`) qui héberge **Stimulsoft Reports.WEB** :

- `POST/GET api/designer?templateId=…` — pont du designer web embarqué (`stimulsoft-designer-angular`)
- `api/templates` — CRUD + versioning des modèles `.mrt` par société et par type de document
- `POST api/render` — rendu PDF serveur : `PdfRenderPayload` (JSON complet) → `application/pdf`
- `POST api/render/preview?templateId=…` — préversion avec les données exemples
- `GET api/schemas/{docType}` — JSON Schema + données exemples (source de vérité des dictionnaires)
- `GET /health` — health check

Types de documents : `Quote`, `Invoice`, `CreditNote`, `SupplierOrder`, `OperationSheet`, `MaintenanceOperationSheet`, `BonLivraison` (alignés sur `TypePdfConfiguration` du webapi Axiobat).

## Lancer en local (mode sandbox)

```bash
cd API
dotnet run
```

- API sur `http://localhost:5290` (Swagger : `/swagger`).
- En Development : **BDD InMemory** (pas de MariaDB requis), **auth désactivée** (`Auth:Enabled=false`, societeId = `dev-societe`), templates stockés dans `API/_data/`.
- Sans clé de licence : Stimulsoft fonctionne en **trial** (filigrane sur les rendus) — suffisant pour le POC.
- Au premier démarrage, un **modèle standard (seed)** est créé pour chacun des 7 types de documents.

L'UI de test est le sandbox du repo `pdf-designer-ui/` (`npm start` → http://localhost:4201).

## Production

- MariaDB (`ConnectionStrings:Default`) + volume persistant pour `Storage:TemplatesRoot`.
- `Stimulsoft__LicenseKey` : clé de licence Reports.WEB (variable d'environnement, jamais commitée).
- `Auth__Enabled=true` + `Auth__SecretKey` : le service valide le **JWT Axiobat existant** (issuer/audience `InovaSquad.com`). Le designer passe le jeton en query string (`?access_token=`).
- `docker compose up` : MariaDB (port 3307) + API (port 5290).

## Multi-tenant

`societeId` est résolu par middleware : claim JWT → header `X-Societe-Id` (Ocelot) → query → valeur dev. Toutes les requêtes templates/render sont scopées par société ; les seeds globaux (SocieteId NULL) sont en lecture seule.
