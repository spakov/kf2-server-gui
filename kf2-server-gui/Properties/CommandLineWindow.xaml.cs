using System;
using System.Windows;
using System.Windows.Controls;

namespace kf2_server_gui.Properties {
  /// <summary>
  /// Interaction logic for CommandLineWindow.xaml
  /// </summary>
  public partial class CommandLineWindow : Window {
    private readonly Uri darkStyleUri = new Uri("/kf2-server-gui;component/Resources/DarkStyle.xaml", UriKind.RelativeOrAbsolute);
    private readonly Uri lightStyleUri = new Uri("/kf2-server-gui;component/Resources/LightStyle.xaml", UriKind.RelativeOrAbsolute);

    /* Constructor */
    public CommandLineWindow(string intro, string command, string random, bool? darkStyle) {
      /* Initialize stuff */
      InitializeComponent();

      /* Set our owner */
      Owner = App.Current.MainWindow;

      /* Set dark style appropriately */
      SetDarkStyle(darkStyle, new Image[] { okImage });

      /* Set the intro, command, and random text */
      introLabel.Text = intro;
      commandTextBox.Text = command;
      randomTextBlock.Text = random;
    }

    /* Enables or disables the dark style */
    private void SetDarkStyle(bool? darkStyle, Image[] darkStyleImages) {
      /* Clear all merged dictionaries to remove the existing style */
      Resources.MergedDictionaries.Clear();

      /* See what the user is requesting */
      if (darkStyle == true) {
        /* Use dark style */
        Resources.MergedDictionaries.Add(new ResourceDictionary() { Source = darkStyleUri });

        /* Use the invert shader on each of our specified images */
        foreach (Image image in darkStyleImages) image.Effect = new InvertEffect();
      } else {
        /* Use light style */
        Resources.MergedDictionaries.Add(new ResourceDictionary() { Source = lightStyleUri });

        /* Use no shader on each of our specified images */
        foreach (Image image in darkStyleImages) image.Effect = null;
      }
    }

    /* The user wants to dismiss the window */
    private void OkButton_Click(object sender, RoutedEventArgs e) => Close();
  }
}
