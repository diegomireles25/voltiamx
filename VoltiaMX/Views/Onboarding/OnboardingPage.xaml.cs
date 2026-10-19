using Microsoft.Extensions.DependencyInjection;
using VoltiaMX.Services;
using VoltiaMX.ViewModels.Onboarding;

namespace VoltiaMX.Views.Onboarding
{
    public sealed partial class OnboardingPage : Page
    {
        private bool _iniciado;

        public OnboardingPage()
        {
            ViewModel = App.Servicios.GetRequiredService<OnboardingViewModel>();
            this.InitializeComponent();
            Loaded += AlCargar;
        }

        public OnboardingViewModel ViewModel { get; }

        private async void AlCargar(object sender, RoutedEventArgs e)
        {
            if (_iniciado)
            {
                return;
            }

            _iniciado = true;
            App.Servicios.GetRequiredService<NavegacionOnboarding>().Marco = MarcoPasos;
            await ViewModel.IniciarAsync();
        }
    }
}
