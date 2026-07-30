# pdf-designer — Moteur de rendu PDF Stimulsoft

Microservice .NET 8 **neutre et sans état** qui héberge **Stimulsoft Reports.WEB**. On lui envoie un
modèle `.mrt`, des données et la liste de ce qu'il doit appliquer dessus ; il renvoie un PDF.

Il ne stocke aucun modèle, ne connaît aucun métier et **n'écrit aucun nom de champ dans son code** :
tout ce qu'il manipule — composants, bandes, colonnes, couleurs — est nommé par l'appelant, seul à
connaître son propre modèle. Deux applications aux modèles de données totalement différents l'utilisent
de la même façon.

- `POST api/render?format=pdf|png` — **le seul endpoint métier**
- `GET|POST api/designer?templateUrl=…` — pont du designer web embarqué (`stimulsoft-designer-angular`)
- `GET /health` — health check

Aucune base de données, aucun disque, aucun cache.

## Le contrat de rendu

```jsonc
POST api/render?format=pdf          // pdf (défaut) | png
{
  "schemaVersion": 2,
  "mrt": "<StiSerializer…>",         // REQUIS — contenu XML du modèle
  "data": { … },                     // forme LIBRE : enregistrée telle quelle comme source du rapport
  "dataSetName": "data",             // défaut "data"
  "fileName": "devis",

  // images posées sur des composants du modèle, désignés par leur nom
  "images": [ { "component": "Logo", "base64": "…", "widthPx": 120, "heightPx": 40 } ],
  "watermark": { "base64": "…", "stretch": true, "transparency": 0 },
  // rangées d'images AJOUTÉES au modèle (aucun composant à alimenter)
  "imageRows": [ { "band": null, "images": ["…"], "maxCount": 4, "sizeCm": 2, "namePrefix": "Badge" } ],

  // remplacements de couleur sur tout le rapport, et styles nommés référençables par les bandes
  "colorReplacements": [ { "from": "#56AAC6", "to": "#E67E22", "tolerance": 6 } ],
  "styles": [ { "name": "AltOddRow", "backColor": "#EAF6FA" } ],

  // mise en page d'une bande : colonnes visibles, largeurs, hauteur de ligne, alternance, bordures
  "bands": [ {
      "name": "Lignes", "headerBand": "EnteteLignes",
      "columnPrefixes": { "header": "HCol_", "data": "DCol_" },
      "columns": [ { "key": "num", "visible": true }, { "key": "qte", "visible": false } ],
      "totalWidthCm": 19, "flexColumn": "designation", "flexMinWidthCm": 3, "rowHeightCm": 0.5,
      "oddStyle": "AltOddRow", "evenStyle": "AltEvenRow",
      "border": { "sides": "all", "color": "#C0C0C0", "width": 1 },
      "headerBorder": { "sides": "none" },
      "borderExclude": [ "RowBg" ]
  } ],

  // retouches ponctuelles, coloration d'une rangée repérée par ce qu'elle affiche
  "componentOverrides": [ { "name": "RowBg", "backColor": "transparent", "clearConditions": true } ],
  "rowFills": [ { "band": "Totaux", "anchorExpression": "totaux.totalHT", "minLeftCm": 10, "backColor": "#EAF6FA" } ],

  // reprise en main d'un modèle personnalisé dont les liaisons ont été figées dans le designer
  "textRewrites": [ { "whenContainsAny": ["{client.siret}"], "skipIfContains": ["{client.bloc}"], "setTo": "{client.bloc}" } ],

  // mise en forme évaluée ligne par ligne — le seul moyen de styler différemment les lignes d'une bande
  "conditions": [ { "band": "Lignes", "excludeComponents": ["RowBg"],
                    "expression": "lignes.type == \"article\"",
                    "fontSize": 8.5, "bold": true, "textColor": "#333333" } ],

  "attachments": [ "…base64…" ]      // PDF ou images concaténés à la fin
}
```

**Une colonne** est un couple de composants portant la même clé derrière deux préfixes, l'un dans la
bande d'en-tête, l'autre dans la bande de données (`HCol_prix` / `DCol_prix`). Masquer une colonne
recompacte les autres vers la gauche ; celle déclarée `flexColumn` absorbe la largeur libérée.

**L'ordre d'application** est fixe et voulu : styles → couleurs → mise en page des bandes → réécritures
de texte → images → retouches ponctuelles → conditions. Les couleurs passent avant la mise en page (qui
pose ses propres bordures), les retouches après elle (elles ont le dernier mot sur un composant), et les
conditions en dernier — elles repartent de la police effective des cellules.

## Où vivent les modèles

**Chez l'appelant.** Le moteur ne détient aucun `.mrt` et ne sait pas où ils sont rangés.

Pour Axiobat : table `pdf_template`, côté `webApi-axiobat1`. C'est l'API Axiobat qui résout le modèle de
la société, enrichit les données, traduit sa configuration en directives et appelle `api/render`
(`POST api/Pdf/Render/{docType}`). Toute la traduction se trouve dans
`Axiobat.Services/Pdf/PdfRenderDirectivesBuilder.cs` — c'est le seul endroit du système qui connaisse à
la fois le métier et ce contrat.

Le générateur des modèles standards Axiobat vit lui aussi là-bas
(`webApi-axiobat1/Tools/PdfTemplateSeedGen`) : le moteur ne génère jamais de modèle.

## Le pont designer

`api/designer` est la seule partie du moteur qui appelle un autre service, parce que le composant
Stimulsoft dialogue directement avec son backend sans passer par le code de la page hôte. L'appelant
fournit `templateUrl` : l'adresse à laquelle lire (`GET`) et écrire (`PUT`) ce `.mrt`. Le jeton de
l'utilisateur est propagé tel quel, et les règles d'accès (modèle en lecture seule, droits) restent
celles du magasin, qui refuse la sauvegarde le cas échéant.

Le magasin peut joindre au modèle les **directives** à lui appliquer — mêmes directives que pour le
rendu — de sorte que l'utilisateur édite une mise en page qui ressemble à son document :

```jsonc
GET {templateUrl}
{ "value": { "mrt": "<StiSerializer…>", "directives": { "colorReplacements": [...], "bands": [...] } } }
```

Le moteur applique alors **tout sauf les images** (`images`, `watermark`, `imageRows`). Ce que le
designer affiche est ce qui sera figé dans le `.mrt` à l'enregistrement : figer un logo ou un papier
en-tête en base64 rendrait le modèle insensible à un changement d'image. Un magasin qui ne fournit pas
de directives reste valide — le modèle est alors affiché brut.

**`TemplateStore:AllowedHosts` est obligatoire.** Une URL librement choisie ferait du moteur un relais
vers tout ce que son réseau peut joindre : la liste blanche est vide par défaut, et sans elle **aucune
URL n'est acceptée**.

| Réglage | Rôle |
|---|---|
| `TemplateStore:AllowedHosts` | hôtes autorisés pour `templateUrl` (obligatoire) |
| `TemplateStore:ResponsePath` | chemin pointé du `.mrt` dans la réponse (défaut `value.mrt`) |
| `TemplateStore:DirectivesPath` | chemin pointé des directives dans la réponse (défaut `value.directives`) |
| `TemplateStore:RequestProperty` | nom de la propriété du corps envoyé en écriture (défaut `mrt`) |

## Lancer en local

```bash
cd API
ASPNETCORE_ENVIRONMENT=Development dotnet run --launch-profile http
```

- API sur `http://localhost:5290` (Swagger : `/swagger`).
- En Development : **auth désactivée** (`Auth:Enabled=false`), hôtes `localhost` / `127.0.0.1` autorisés.
- Sans clé de licence : Stimulsoft fonctionne en **trial** (filigrane sur les rendus).
- Arrêter le service avant de rebuilder, sinon `MSB3021` : `API.exe` reste verrouillé.

## Production

- Aucun volume, aucune base : le conteneur est sans état et scalable horizontalement.
- `Stimulsoft__LicenseKey` : clé de licence Reports.WEB (variable d'environnement, jamais commitée).
- `Auth__Enabled=true` + `Auth__SecretKey`, `Auth__Issuer`, `Auth__Audience` : le moteur valide le JWT de
  l'application appelante ; il n'en émet aucun.
- `TemplateStore__AllowedHosts__0` : au moins un hôte, sinon le pont designer refuse tout.
- `Render__MaxConcurrency` / `Render__TimeoutSeconds` : un rendu Stimulsoft est coûteux en mémoire, les
  laisser tous partir en même temps ferait tomber le service.
- `docker compose up` : API seule (port 5290).
