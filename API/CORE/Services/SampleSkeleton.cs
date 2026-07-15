namespace API.CORE.Services
{
    /// <summary>
    /// Squelette de donnees GENERIQUE, construit EN CODE (aucun fichier .json embarque).
    /// Sert uniquement a doter les modeles .mrt d'un dictionnaire de champs :
    ///   - construction des seeds au demarrage / creation d'un modele societe,
    ///   - editeur avance (liste des champs disponibles),
    ///   - repli de l'apercu quand l'appelant (Axiobat) n'envoie pas de donnees.
    /// Les VRAIES donnees d'apercu et de rendu sont fournies par l'appelant (voir RenderController.Preview
    /// `DataJson`, et POST /api/render). Chaque tableau contient au moins une ligne entierement renseignee
    /// pour que Stimulsoft (StiJsonToDataSetConverter) infere correctement les colonnes.
    /// </summary>
    public static class SampleSkeleton
    {
        // Vignette : petit PNG base64 valide (16x16) pour la colonne image des lignes.
        private const string Vignette =
            "iVBORw0KGgoAAAANSUhEUgAAABAAAAAQCAIAAACQkWg2AAAAJUlEQVR4nGPQqLijUXHnPxgQw2YgWQPxSiFs0jWM+mHUD1TyAwCAZDyfPzU7hQAAAABJRU5ErkJggg==";

        /// <summary>
        /// Retourne le squelette generique. Le parametre docType est accepte pour d'eventuelles variantes
        /// futures, mais le squelette universel couvre toutes les tables liees par les seeds (lignes,
        /// equipements, fournisseur, echeances, detailTva…) ; chaque modele ne lie que celles qu'il utilise.
        /// </summary>
        public static string GetJson(string? docType = null) => Universal;

        private static readonly string Universal = $$"""
        {
          "document": {
            "reference": "REF-0000",
            "titre": "Document",
            "sousTitre": "Apercu",
            "objet": "Exemple de rendu — donnees fournies par l'application appelante.",
            "dateCreation": "2026-01-01",
            "dateValidite": "2026-02-01",
            "dateEcheance": "2026-02-01",
            "statut": "Brouillon",
            "versionDevis": "V1",
            "notes": "Ligne de note d'exemple.",
            "observations": "Observation d'exemple.",
            "client": {
              "civilite": "M.",
              "nom": "Client Exemple",
              "email": "client@exemple.fr",
              "telephone": "01 23 45 67 89",
              "siret": "000 000 000 00000",
              "adresseFacturation": { "rue": "1 rue de l'Exemple", "codePostal": "75000", "ville": "Paris", "pays": "FR" },
              "adresseChantier":    { "rue": "1 rue de l'Exemple", "codePostal": "75000", "ville": "Paris", "pays": "FR" },
              "adresseLivraison":   { "rue": "1 rue de l'Exemple", "codePostal": "75000", "ville": "Paris", "pays": "FR" }
            },
            "fournisseur": {
              "nom": "Fournisseur Exemple",
              "email": "fournisseur@exemple.fr",
              "telephone": "01 23 45 67 89",
              "siret": "000 000 000 00000",
              "adresse": { "rue": "2 rue du Fournisseur", "codePostal": "75000", "ville": "Paris", "pays": "FR" }
            },
            "lignes": [
              { "type": "lot",     "numero": "1",   "designation": "Lot d'exemple",       "quantite": 1, "unite": "U", "prixUnitaire": 0,    "remise": 0, "tva": 20, "totalHT": 0,    "totalTTC": 0,    "quantiteCommandee": 1, "quantiteLivree": 1, "quantiteRestante": 0, "vignette": "{{Vignette}}" },
              { "type": "ouvrage", "numero": "1.1", "designation": "Ouvrage d'exemple",   "quantite": 1, "unite": "U", "prixUnitaire": 100,  "remise": 0, "tva": 20, "totalHT": 100,  "totalTTC": 120,  "quantiteCommandee": 1, "quantiteLivree": 1, "quantiteRestante": 0, "vignette": "{{Vignette}}" },
              { "type": "article", "numero": "1.2", "designation": "Article d'exemple A", "quantite": 2, "unite": "U", "prixUnitaire": 50,   "remise": 0, "tva": 20, "totalHT": 100,  "totalTTC": 120,  "quantiteCommandee": 2, "quantiteLivree": 2, "quantiteRestante": 0, "vignette": "{{Vignette}}" },
              { "type": "article", "numero": "1.3", "designation": "Article d'exemple B", "quantite": 3, "unite": "U", "prixUnitaire": 30,   "remise": 0, "tva": 20, "totalHT": 90,   "totalTTC": 108,  "quantiteCommandee": 3, "quantiteLivree": 3, "quantiteRestante": 0, "vignette": "{{Vignette}}" }
            ],
            "totaux": {
              "totalHT": 290,
              "remiseGlobale": 0,
              "detailTva": [ { "taux": 20, "base": 290, "montant": 58 } ],
              "totalTTC": 348,
              "acompte": 0,
              "netAPayer": 348,
              "totalEnLettres": "Trois cent quarante-huit euros",
              "totalTva": 58
            },
            "paiement": {
              "conditions": "30% a la commande, solde a la reception",
              "rib": "FR76 0000 0000 0000 0000 0000 000",
              "echeances": [ { "libelle": "Acompte", "date": "2026-01-15", "montant": 104.4 } ]
            },
            "equipements": [
              { "designation": "Equipement d'exemple", "marque": "Marque", "modele": "Modele", "numeroSerie": "SN-0000" }
            ]
          },
          "societe": {
            "nom": "Ma Societe",
            "email": "contact@masociete.fr",
            "telephone": "01 23 45 67 89",
            "siteWeb": "www.masociete.fr",
            "siret": "000 000 000 00000",
            "tvaIntracommunautaire": "FR 00 000000000",
            "capital": "10 000 EUR",
            "adresse": { "rue": "3 rue de la Societe", "codePostal": "75000", "ville": "Paris", "pays": "France" },
            "mentionsLegales": "Mentions legales d'exemple.",
            "pied": "Ma Societe - 3 rue de la Societe, 75000 Paris - Tel : 01 23 45 67 89\nRCS Paris 000 000 000 - SIRET 000 000 000 00000 - TVA FR 00 000000000\nwww.masociete.fr - Capital 10 000 EUR"
          },
          "options": {
            "titreDocument": "DOCUMENT",
            "sousTitreDocument": "",
            "masquerPrix": false,
            "lienStripe": "",
            "zoneSignature": true,
            "mentionsSpecifiques": "Bon pour accord (date et signature)"
          }
        }
        """;
    }
}
