using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.UI.Popups;

// Pour en savoir plus sur le modèle d’élément Page vierge, consultez la page http://go.microsoft.com/fwlink/?LinkID=390556

namespace Yana
{
    /// <summary>
    /// Une page vide peut être utilisée seule ou constituer une page de destination au sein d'un frame.
    /// </summary>
    public sealed partial class configPage : Page
    {
        Windows.Storage.ApplicationDataContainer localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;

        public configPage()
        {
            this.InitializeComponent();          

            if (localSettings.Values["first"] != null)
            {
                serverExt.Text = localSettings.Values["savedServerExt"].ToString();
                serverInt.Text = localSettings.Values["savedServerInt"].ToString();
                token.Text = localSettings.Values["savedToken"].ToString();
                port.Text = localSettings.Values["savedPort"].ToString();

                if ((bool)localSettings.Values["tts"])
                {
                    tts.IsOn=true;
                }
                else tts.IsOn = false;

            }
            localSettings.Values["first"] = false;
        }

        /// <summary>
        /// Invoqué lorsque cette page est sur le point d'être affichée dans un frame.
        /// </summary>
        /// <param name="e">Données d'événement décrivant la manière dont l'utilisateur a accédé à cette page.
        /// Ce paramètre est généralement utilisé pour configurer la page.</param>
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
        }

        private async void saveConf_Click(object sender, RoutedEventArgs e)
        {
            if(serverExt.Text == "" && token.Text == "" && port.Text == "" && serverInt.Text == "")
            {
                MessageDialog msgbox = new MessageDialog("Un ou plusieurs champs sont vide...");
                await msgbox.ShowAsync(); 
            }
            else
            {
                localSettings.Values["savedServerExt"] = serverExt.Text;
                localSettings.Values["savedServerInt"] = serverInt.Text;
                localSettings.Values["savedToken"] = token.Text;
                localSettings.Values["savedPort"] = port.Text;

                if (tts.IsOn)
                {
                    localSettings.Values["tts"] = true;
                }
                else localSettings.Values["tts"] = false;

                Frame.Navigate(typeof(PageAction));
            }
            
           
        }
    }
}
