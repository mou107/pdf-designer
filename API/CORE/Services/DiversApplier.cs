using Stimulsoft.Report;
using Stimulsoft.Report.Components;

namespace API.CORE.Services
{
    /// <summary>
    /// Normalise au rendu les bindings « identite client » d'un .mrt vers les variables calculees par
    /// <see cref="DiversStyler"/> ({client.blocClient} / {client.affaire} / {client.adresseInter}). Ainsi les
    /// options « Divers » pilotent le bloc client MEME sur un modele deja personnalise dans l'editeur avance
    /// (dont le .mrt fige les anciens champs en dur). Fonctionne comme ColumnsApplier : sur le rapport charge,
    /// avant Render(). Les composants deja sur la variable sont ignores (idempotent) — l'utilisateur peut
    /// deplacer / restyler le bloc dans le designer, Divers garde le controle du contenu.
    /// </summary>
    public static class DiversApplier
    {
        public static void Apply(StiReport report)
        {
            foreach (StiPage page in report.Pages)
                Walk(page.Components);
        }

        private static void Walk(StiComponentsCollection comps)
        {
            foreach (StiComponent c in comps)
            {
                if (c is StiText t) Normalize(t);
                if (c is StiContainer cont && cont.Components is { Count: > 0 })
                    Walk(cont.Components);
            }
        }

        private static void Normalize(StiText t)
        {
            var v = t.Text?.Value;
            if (string.IsNullOrEmpty(v)) return;

            // Deja sur une variable calculee -> idempotent.
            if (v.Contains("{client.blocClient}") || v.Contains("{client.affaire}") || v.Contains("{client.adresseInter}") || v.Contains("{societe.pied}"))
                return;

            // Pied de page : mentions legales -> variable pied (3 lignes configurees), meme sur .mrt personnalise.
            if (v.Contains("{societe.mentionsLegales}"))
            {
                t.Text.Value = "{societe.pied}";
                return;
            }

            // Affaire suivie par… (contient {client.email} avec le libelle) -> variable affaire.
            if (v.Contains("Affaire suivie"))
            {
                t.Text.Value = "{client.affaire}";
                return;
            }

            // Adresse d'intervention (adresse chantier) -> variable adresse intervention.
            if (v.Contains("Adresse d'intervention") || v.Contains("{adresseChantier"))
            {
                t.Text.Value = "{client.adresseInter}";
                return;
            }

            // Bloc identite client (civilite + nom, SIRET, ou adresse de facturation) -> variable bloc client.
            // La boite « Client » qui ne montre que {client.nom} n'est PAS reecrite (pas de ces marqueurs).
            if (v.Contains("{adresseFacturation") || v.Contains("{client.siret}") ||
                (v.Contains("{client.civilite}") && v.Contains("{client.nom}")))
            {
                t.Text.Value = "{client.blocClient}";
            }
        }
    }
}
