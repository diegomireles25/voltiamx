using Microsoft.UI.Xaml.Navigation;
using VoltiaMX.ViewModels.Onboarding;

namespace VoltiaMX.Views.Onboarding
{
    public sealed partial class BienvenidaPage : Page
    {
        public BienvenidaPage()
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
