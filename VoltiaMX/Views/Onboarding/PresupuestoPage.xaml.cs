using Microsoft.UI.Xaml.Navigation;
using VoltiaMX.ViewModels.Onboarding;

namespace VoltiaMX.Views.Onboarding
{
    public sealed partial class PresupuestoPage : Page
    {
        public PresupuestoPage()
        {
            this.InitializeComponent();
        }

        public OnboardingViewModel ViewModel { get; private set; } = null!;

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            ViewModel = (OnboardingViewModel)e.Parameter;
            base.OnNavigatedTo(e);
        }
    }
}
