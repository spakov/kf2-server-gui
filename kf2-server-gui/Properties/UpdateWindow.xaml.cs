using IniParser.Model;
using Microsoft.WindowsAPICodePack.Dialogs;
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Controls;

namespace kf2_server_gui.Properties {
  /// <summary>
  /// Interaction logic for UpdateWindow.xaml
  /// </summary>
  public partial class UpdateWindow : Window {
    readonly string appName;
    readonly string kf2Dir;

    private readonly IniData iniData;
    private readonly IniData newIniData;

    const string SteamcmdFilter1 = "Executables";
    const string SteamcmdFilter2 = "*.exe";
    const string SteamcmdNameSubstring = "steamcmd";

    const string FormatString2 = "{0} {1}";
    const string FormatString3 = "{0} {1} {2}";
    const string Kf2GameId = "232130";
    const string ForceInstallDir = "force_install_dir";
    const string AppUpdate = "app_update";
    const string Validate = "validate";
    const string ValidScriptRegex = @"^\s*" + AppUpdate + @"\s*" + Kf2GameId + ".*$";
    const string ForceInstallDirRegex = @"^\s*" + ForceInstallDir + ".*$";
    const string AppUpdateRegex = @"^\s*" + AppUpdate + ".*$";

    const string RunScript = "+runscript";
    const string UpdateScript = "update.script";

    readonly bool initializing = true;

    bool validPath = false;
    bool validScript = false;

    Process updateProcess;

    private readonly Uri darkStyleUri = new Uri("/kf2-server-gui;component/Resources/DarkStyle.xaml", UriKind.RelativeOrAbsolute);
    private readonly Uri lightStyleUri = new Uri("/kf2-server-gui;component/Resources/LightStyle.xaml", UriKind.RelativeOrAbsolute);

    private readonly Image[] darkStyleImages;

    /* Constructor */
    public UpdateWindow(string appName, string kf2Dir, ref IniData iniData, ref IniData newIniData, bool? darkStyle) {
      /* Initialize stuff */
      InitializeComponent();

      /* Set our owner */
      Owner = App.Current.MainWindow;

      /* Note our .ini configuration */
      this.appName = appName;
      this.kf2Dir = kf2Dir;
      this.iniData = iniData;
      this.newIniData = newIniData;

      /* Set up images for style handling */
      darkStyleImages = new Image[] {
        updateImage,
        okImage
      };

      /* Set dark style appropriately */
      SetDarkStyle(darkStyle);

      /* Load our configuration from our .ini file data that was passed along */
      LoadConfiguration();

      /* We're done initializing */
      initializing = false;

      /* Validate our configuration and update control states */
      ValidateConfiguration();
    }

    /* Loads the configuration from our .ini file data */
    private void LoadConfiguration() {
      string updateScript;

      /* Set the steamcmd executable path */
      steamcmdPathTextBox.Text = newIniData[appName][IniConstants.SteamcmdPath] ?? iniData[appName][IniConstants.SteamcmdPath];

      /* Set the update script by trying newIniData first */
      updateScript = newIniData[appName][IniConstants.UpdateScript];

      /* Then fall back to the regular iniData */
      if (updateScript == null) updateScript = iniData[appName][IniConstants.UpdateScript];

      /* If that came out null, we'll use the default script instead */
      if (updateScript == null) {
        updateScript = Strings.DefaultUpdateScript;
      } else {
        /* The value in the .ini file is Base64 encoded, so let's decode it */
        updateScript = Encoding.UTF8.GetString(Convert.FromBase64String(updateScript));
      }

      /* Replace the KF2 server directory with the configured one */
      updateScript = Regex.Replace(updateScript, ForceInstallDirRegex, string.Format(FormatString2, ForceInstallDir, kf2Dir), RegexOptions.Multiline);

      /* Finally, set our text box to contain the correct script */
      updateScriptTextBox.Text = updateScript;

      /* Set the validate checkbox state */
      if ((newIniData[appName][IniConstants.Validate] ?? iniData[appName][IniConstants.Validate]) == IniConstants.True) validateCheckBox.IsChecked = true;
    }

    /* Saves the configuration to our .ini file */
    private void SaveConfiguration() {
      /* Save the steamcmd executable path */
      newIniData[appName][IniConstants.SteamcmdPath] = steamcmdPathTextBox.Text;

      /* Save the update script in Base64 */
      newIniData[appName][IniConstants.UpdateScript] = Convert.ToBase64String(Encoding.UTF8.GetBytes(updateScriptTextBox.Text));

      /* Save the validate checkbox state */
      newIniData[appName][IniConstants.Validate] = validateCheckBox.IsChecked == true ? IniConstants.True : IniConstants.False;
    }

    /* Allows the main window to save configuration related to the update window.
     * This is needed if the user never opened the update window. */
    public static void ExternalSaveConfiguration(string appName, ref IniData iniData, ref IniData newIniData) {
      string[] keys = { IniConstants.SteamcmdPath, IniConstants.UpdateScript, IniConstants.Validate };

      /* Do this for each key we touch in UpdateWindow */
      foreach (string key in keys) {
        /* If the newIniData key is already set, we don't need to do anything */
        if (newIniData[appName][key] == null) {
          /* Copy the old iniData value to the newIniData */
          if (iniData[appName][key] != null) newIniData[appName][key] = iniData[appName][key];
        }
      }
    }

    /* Validates the current configuration, which includes updating button states and status bar text */
    private void ValidateConfiguration() {
      validPath = true;
      validScript = true;

      /* If we're still initializing, stop now */
      if (initializing) return;

      /* See if the steamcmd executable exists */
      if (!File.Exists(steamcmdPathTextBox.Text)) validPath = false;

      /* If the path is valid, update our status bar text */
      if (!validPath) {
        /* Update our button states */
        updateButton.IsEnabled = false;

        /* Update the status bar text */
        statusStatusBarItem.Content = Strings.BadSteamcmdPath;

        return;
      } else {
        /* Update our button states */
        updateButton.IsEnabled = true;
      }

      /* See if the script seems valid */
      if (!Regex.IsMatch(updateScriptTextBox.Text, ValidScriptRegex, RegexOptions.Multiline)) validScript = false;

      /* If the script is not valid, update our status bar text */
      if (!validScript) {
        /* Update our button states */
        updateButton.IsEnabled = false;

        /* Update the status bar text */
        statusStatusBarItem.Content = string.Format(Strings.BadUpdateScript, Kf2GameId);

        return;
      } else {
        /* Update our button states */
        updateButton.IsEnabled = true;
      }

      /* Save the configuration */
      SaveConfiguration();

      /* See if the server is running */
      if (updateProcess != null && !updateProcess.HasExited) {
        /* Update our button states */
        updateButton.IsEnabled = false;

        /* Update the status bar text */
        statusStatusBarItem.Content = Strings.UpdatingServer;
      } else {
        /* Update our button states */
        updateButton.IsEnabled = true;

        /* Update the status bar text */
        statusStatusBarItem.Content = Strings.ReadyToUpdate;
      }
    }

    /* Builds the command line required to start the update */
    private string BuildCommandLine() {
      StringBuilder commandLine = new StringBuilder();

      /* Build the command line */
      commandLine.Append(RunScript);
      commandLine.Append(" ");
      commandLine.Append(kf2Dir);
      commandLine.Append(System.IO.Path.DirectorySeparatorChar);
      commandLine.Append(UpdateScript);

      return commandLine.ToString();
    }

    /* Starts the server update process */
    private void StartUpdate() {
      updateProcess = new Process();

      /* Write our update script to the KF2 server directory */
      try {
        File.WriteAllText(kf2Dir + System.IO.Path.DirectorySeparatorChar + UpdateScript, updateScriptTextBox.Text);
      } catch (Exception) {
        MessageBox.Show(Strings.CouldNotWriteUpdateScript, Strings.CouldNotWriteUpdateScriptCaption, MessageBoxButton.OK, MessageBoxImage.Error);
        return;
      }

      /* Set up for executing the server */
      updateProcess.StartInfo.UseShellExecute = false;
      updateProcess.StartInfo.FileName = steamcmdPathTextBox.Text;
      updateProcess.StartInfo.Arguments = BuildCommandLine();
      updateProcess.EnableRaisingEvents = true;
      updateProcess.Exited += UpdateProcess_Exited;

      /* Kick off the process */
      if (updateProcess.Start()) {
        /* Update window elements appropriately */
        ValidateConfiguration();
      }
    }

    /* Enables or disables the dark style */
    private void SetDarkStyle(bool? darkStyle) {
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

    /* The update process terminated */
    private void UpdateProcess_Exited(object sender, EventArgs e) => Dispatcher.BeginInvoke(new ThreadStart(() => ValidateConfiguration()));

    /* The user changed the text in the steamcmd path text box */
    private void SteamcmdPathTextBox_TextChanged(object sender, TextChangedEventArgs e) => ValidateConfiguration();

    /* The user wants to browse for the steamcmd executable */
    private void SteamcmdPathBrowseButton_Click(object sender, RoutedEventArgs e) {
      /* Show a file picker */
      using (CommonOpenFileDialog commonOpenFileDialog = new CommonOpenFileDialog()) {
        commonOpenFileDialog.Filters.Add(new CommonFileDialogFilter(SteamcmdFilter1, SteamcmdFilter2));
        CommonFileDialogResult dialogResult = commonOpenFileDialog.ShowDialog();

        if (dialogResult == CommonFileDialogResult.Ok && !string.IsNullOrWhiteSpace(commonOpenFileDialog.FileName) && commonOpenFileDialog.FileName.Contains(SteamcmdNameSubstring)) {
          steamcmdPathTextBox.Text = commonOpenFileDialog.FileName;
        }
      }
    }

    /* The user changed the text in the update script text box */
    private void UpdateScriptTextBox_TextChange(object sender, RoutedEventArgs e) => ValidateConfiguration();

    /* The user wants to validate the server files after downloading */
    private void ValidateCheckBox_Checked(object sender, RoutedEventArgs e) {
      /* Update the script appropriately */
      updateScriptTextBox.Text = Regex.Replace(updateScriptTextBox.Text, AppUpdateRegex, string.Format(FormatString3, AppUpdate, Kf2GameId, Validate), RegexOptions.Multiline);

      /* Kick off a validation as well */
      ValidateConfiguration();
    }

    /* The user does not want to validate the server files after downloading */
    private void ValidateCheckBox_Unchecked(object sender, RoutedEventArgs e) {
      /* Update the script appropriately */
      updateScriptTextBox.Text = Regex.Replace(updateScriptTextBox.Text, AppUpdateRegex, string.Format(FormatString2, AppUpdate, Kf2GameId), RegexOptions.Multiline);

      /* Kick off a validation as well */
      ValidateConfiguration();
    }

    /* The user wants to update the server */
    private void UpdateButton_Click(object sender, RoutedEventArgs e) => StartUpdate();

    /* The user wants to close the window */
    private void OkButton_Click(object sender, RoutedEventArgs e) => Close();
  }
}
