using Microsoft.UI.Xaml.Automation;
using Microsoft.Windows.ApplicationModel.Resources;

namespace VoltiaMX.Views
{
    /// <summary>
    /// Enlace "¿Qué es esto?" con la explicación de un término. <see cref="Clave"/> elige los textos
    /// Ayuda_{Clave} (la explicación) y Ayuda_Termino_{Clave} (el término, para el nombre accesible).
    /// </summary>
    public sealed partial class TerminoAyuda : UserControl
    {
        private static readonly ResourceLoader Recursos = new();

        public static readonly DependencyProperty ClaveProperty = DependencyProperty.Register(
            nameof(Clave), typeof(string), typeof(TerminoAyuda), new PropertyMetadata(null, (d, _) => ((TerminoAyuda)d).Cargar()));

        public TerminoAyuda()
        {
            this.InitializeComponent();
        }

        public string? Clave
        {
            get => (string?)GetValue(ClaveProperty);
            set => SetValue(ClaveProperty, value);
        }

        private void Cargar()
        {
            if (string.IsNullOrEmpty(Clave))
            {
                return;
            }

            var enlace = Recursos.GetString("Ayuda_QueEsEsto");
            var explicacion = Recursos.GetString($"Ayuda_{Clave}");
            Enlace.Content = enlace;
            Explicacion.Text = explicacion;
            AutomationProperties.SetName(Enlace, $"{enlace} {Recursos.GetString($"Ayuda_Termino_{Clave}")}");
            AutomationProperties.SetHelpText(Enlace, explicacion);
        }
    }
}
