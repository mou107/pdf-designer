using System.Drawing;
using API.CORE.Schemas;
using Stimulsoft.Base.Drawing;
using Stimulsoft.Report;
using Stimulsoft.Report.Components;

namespace API.CORE.Services.Report
{
    /// <summary>
    /// Retouches ponctuelles apres la mise en page : un composant designe par son nom, ou une rangee
    /// reperee par l'expression qu'elle affiche.
    /// </summary>
    internal static class ComponentOverrideApplier
    {
        /// <summary>
        /// Applique les retouches nommees. Elles passent EN DERNIER sur les composants : c'est ce qui
        /// permet de neutraliser un element pose par le modele (un fond de ligne opaque, par exemple)
        /// sans toucher au reste de la mise en page.
        /// </summary>
        public static void Apply(StiReport report, IEnumerable<ComponentOverride>? overrides)
        {
            var byName = (overrides ?? Enumerable.Empty<ComponentOverride>())
                .Where(item => !string.IsNullOrWhiteSpace(item.Name))
                .ToLookup(item => item.Name!, StringComparer.Ordinal);

            if (byName.Count == 0) return;

            foreach (var component in ReportPrimitives.AllComponents(report))
            {
                if (component.Name == null) continue;

                foreach (var directive in byName[component.Name])
                    ApplyOne(component, directive);
            }
        }

        private static void ApplyOne(StiComponent component, ComponentOverride directive)
        {
            if (component is StiText text)
            {
                if (ReportPrimitives.ParseColor(directive.BackColor) is Color back)
                    text.Brush = new StiSolidBrush(back);
                if (ReportPrimitives.ParseColor(directive.TextColor) is Color fore)
                    text.TextBrush = new StiSolidBrush(fore);
                if (directive.ClearConditions) text.Conditions?.Clear();
            }

            if (directive.HeightCm is > 0) component.Height = directive.HeightCm.Value;
            if (directive.WidthCm is > 0) component.Width = directive.WidthCm.Value;
            if (directive.Enabled.HasValue) component.Enabled = directive.Enabled.Value;
        }

        /// <summary>
        /// Colorie une rangee sans que l'appelant ait a connaitre les coordonnees : la rangee est reperee
        /// par le composant qui affiche <c>anchorExpression</c>, et tous les textes places a la meme
        /// hauteur prennent le fond demande.
        /// </summary>
        /// <remarks>
        /// Les blocs de bas de page melangent souvent deux colonnes independantes (des mentions a gauche,
        /// des montants a droite) : <c>minLeftCm</c> restreint la coloration a la partie voulue.
        /// </remarks>
        public static void ApplyRowFills(StiReport report, IEnumerable<RowFillDirective>? directives)
        {
            foreach (var directive in directives ?? Enumerable.Empty<RowFillDirective>())
            {
                if (string.IsNullOrWhiteSpace(directive.AnchorExpression)) continue;

                var color = ReportPrimitives.ParseColor(directive.BackColor);
                if (color == null) continue;

                foreach (var container in ReportPrimitives.ContainersStartingWith(report, directive.Band).ToList())
                {
                    var anchorTop = FindAnchorTop(container, directive.AnchorExpression!);
                    if (anchorTop == null) continue;

                    foreach (StiComponent component in container.Components)
                    {
                        if (component is not StiText text) continue;
                        if (component.Left < directive.MinLeftCm) continue;
                        if (Math.Abs(component.Top - anchorTop.Value) > directive.ToleranceCm) continue;

                        text.Brush = new StiSolidBrush(color.Value);
                    }
                }
            }
        }

        private static double? FindAnchorTop(StiContainer container, string expression)
        {
            foreach (StiComponent component in container.Components)
                if (ReportPrimitives.TextValue(component).Contains(expression, StringComparison.Ordinal))
                    return component.Top;

            return null;
        }
    }
}
