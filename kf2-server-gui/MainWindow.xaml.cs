using IniParser;
using IniParser.Model;
using kf2_server_gui.Properties;
using Microsoft.WindowsAPICodePack.Dialogs;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace kf2_server_gui
{
  /// <summary>
  /// Interaction logic for MainWindow.xaml
  /// </summary>
  public partial class MainWindow : Window {
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool AttachConsole(uint dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
    static extern bool FreeConsole();

    [DllImport("kernel32.dll")]
    static extern bool SetConsoleCtrlHandler(ConsoleCtrlDelegate HandlerRoutine, bool Add);

    delegate bool ConsoleCtrlDelegate(CtrlTypes CtrlType);

    // Enumerated type for the control messages sent to the handler routine
    enum CtrlTypes : uint {
      CTRL_C_EVENT = 0,
      CTRL_BREAK_EVENT,
      CTRL_CLOSE_EVENT,
      CTRL_LOGOFF_EVENT = 5,
      CTRL_SHUTDOWN_EVENT
    }

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GenerateConsoleCtrlEvent(CtrlTypes dwCtrlEvent, uint dwProcessGroupId);

    /* Mono doesn't support WPF, but maybe someday */
    readonly string ServerExecutable =
      "Binaries" + Path.DirectorySeparatorChar + "Win64" + Path.DirectorySeparatorChar
      + (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? "KFGameSteamServer.bin.x86_64" : "KFServer.exe");
    readonly string MapsDirectory = "KFGame" + Path.DirectorySeparatorChar + "BrewedPC" + Path.DirectorySeparatorChar + "Maps";
    const string MapsFilter = "KF-*.kfm";

    const string CommandEndlessMode = "?Game=KFGameContent.KFGameInfo_Endless";
    const string CommandWeeklyMode = "?Game=KFGameContent.KFGameInfo_WeeklySurvival";
    const string CommandVersusMode = "?Game=KFGameContent.KFGameInfo_VersusSurvival";
    const string CommandDifficulty = "?Difficulty=";
    const string CommandMaxPlayers = "?MaxPlayers=";
    const string CommandGameLength = "?GameLength=";
    const string CommandAdminName = "?AdminName=";
    const string CommandAdminPassword = "?AdminPassword=";

    readonly bool initializing = true;

    bool validPath = false;

    readonly static string appName = System.Reflection.Assembly.GetExecutingAssembly().GetName().Name;
    readonly string iniFile = appName + ".ini";
    FileIniDataParser iniParser;
    IniData iniData;
    IniData newIniData = new IniData();

    Process serverProcess;

    private readonly Uri darkStyleUri = new Uri("/kf2-server-gui;component/Resources/DarkStyle.xaml", UriKind.RelativeOrAbsolute);
    private readonly Uri lightStyleUri = new Uri("/kf2-server-gui;component/Resources/LightStyle.xaml", UriKind.RelativeOrAbsolute);

    private readonly Image[] darkStyleImages;

    /* Constructor */
    public MainWindow() {
      /* Initialize stuff */
      InitializeComponent();

      /* Do some initial configuration */
      InitialConfiguration();

      /* Set up images for style handling */
      darkStyleImages = new Image[] {
        kf2DirBrowseImage,
        mapRefreshImage,
        commandLineImage,
        startImage,
        stopImage,
        restartImage,
        updateImage,
        closeImage
      };

      /* Load our configuration from our .ini file */
      LoadConfiguration();

      /* We're done initializing */
      initializing = false;

      /* Validate our configuration and update control states */
      ValidateConfiguration();

      /* Load our maps */
      LoadMaps();

      /* Select the correct map from our configuration */
      SelectConfigurationMap();
    }

    /* Do some initial configuration */
    private void InitialConfiguration() {
      System.Reflection.AssemblyName assemblyName = System.Reflection.Assembly.GetExecutingAssembly().GetName();

      /* Update the KF2 logo to show some version information in a tool tip */
      kf2Logo.ToolTip = String.Format(Strings.VersionString, assemblyName.Name, assemblyName.Version.ToString());
    }

    /* Loads the configuration from our .ini file */
    private void LoadConfiguration() {
      /* Parse our .ini file */
      iniParser = new IniParser.FileIniDataParser();
      try {
        iniData = iniParser.ReadFile(iniFile);
      } catch (Exception) {
        iniData = new IniData();

        /* Set the default admin username and password */
        iniData[appName][IniConstants.AdminUsername] = Strings.DefaultAdminUsername;
        iniData[appName][IniConstants.AdminPassword] = Strings.DefaultAdminPassword;
        newIniData[appName][IniConstants.AdminUsername] = iniData[appName][IniConstants.AdminUsername];
        newIniData[appName][IniConstants.AdminPassword] = iniData[appName][IniConstants.AdminPassword];
      }

      /* Set the KF2 server directory */
      kf2DirTextBox.Text = iniData[appName][IniConstants.ServerFolder];

      /* Set the game mode */
      switch (iniData[appName][IniConstants.GameMode]) {
        case IniConstants.Endless:
          endlessRadioButton.IsChecked = true;
          break;
        case IniConstants.WeeklyOutbreaks:
          weeklyRadioButton.IsChecked = true;
          break;
        case IniConstants.VersusSurvival:
          versusRadioButton.IsChecked = true;
          break;
      }

      /* Set the difficulty */
      switch (iniData[appName][IniConstants.Difficulty]) {
        case IniConstants.Hard:
          hardRadioButton.IsChecked = true;
          break;
        case IniConstants.Suicidal:
          suicidalRadioButton.IsChecked = true;
          break;
        case IniConstants.HellOnEarth:
          hellOnEarthRadioButton.IsChecked = true;
          break;
      }

      /* Set the player count */
      double.TryParse(iniData[appName][IniConstants.MaxPlayers], out double playerCount);
      if (playerCount != 0) playerCountSlider.Value = playerCount;

      /* Set the game length */
      switch (iniData[appName][IniConstants.GameLength]) {
        case IniConstants.Short:
          shortRadioButton.IsChecked = true;
          break;
        case IniConstants.Long:
          longRadioButton.IsChecked = true;
          break;
      }

      /* Set the admin username and password */
      adminUsernameTextBox.Text = iniData[appName][IniConstants.AdminUsername];
      adminPasswordPasswordBox.Password = iniData[appName][IniConstants.AdminPassword];

      /* Set the extra options */
      extraOptionsTextBox.Text = iniData[appName][IniConstants.ExtraOptions];

      /* Set the dark style */
      if (iniData[appName][IniConstants.DarkStyle] == IniConstants.False) darkStyleCheckBox.IsChecked = false;
    }

    /* Selects the correct map based on our configuration (or tries to) */
    private void SelectConfigurationMap() {
      /* See if any maps are loaded */
      if (mapsComboBox.Items.Count == 0) return;

      /* Set the map */
      if (iniData[appName][IniConstants.StartingMap] == IniConstants.Random) {
        /* Select the top entry, which is a random map */
        mapsComboBox.SelectedIndex = 0;
      } else {
        /* Select the entry from our configuration */
        mapsComboBox.Text = iniData[appName][IniConstants.StartingMap];
      }
    }

    /* Saves the configuration to our .ini file */
    private void SaveConfiguration() {
      /* If we're not initialized yet, stop trying to do things */
      if (iniData == null) return;

      /* Save the KF2 server directory */
      newIniData[appName][IniConstants.ServerFolder] = kf2DirTextBox.Text;

      /* Save the game mode */
      if (survivalRadioButton.IsChecked == true) {
        newIniData[appName][IniConstants.GameMode] = IniConstants.Survival;
      } else if (endlessRadioButton.IsChecked == true) {
        newIniData[appName][IniConstants.GameMode] = IniConstants.Endless;
      } else if (weeklyRadioButton.IsChecked == true) {
        newIniData[appName][IniConstants.GameMode] = IniConstants.WeeklyOutbreaks;
      } else if (versusRadioButton.IsChecked == true) {
        newIniData[appName][IniConstants.GameMode] = IniConstants.VersusSurvival;
      }

      /* Save the map */
      if (mapsComboBox.SelectedValue != null) {
        /* See if this is a random map */
        if (mapsComboBox.SelectedIndex == 0) {
          newIniData[appName][IniConstants.StartingMap] = IniConstants.Random;
        } else {
          newIniData[appName][IniConstants.StartingMap] = ((ComboBoxItem) mapsComboBox.SelectedValue).Content.ToString();
        }
      }

      /* Save the difficulty */
      if (normalRadioButton.IsChecked == true) {
        newIniData[appName][IniConstants.Difficulty] = IniConstants.Normal;
      } else if (hardRadioButton.IsChecked == true) {
        newIniData[appName][IniConstants.Difficulty] = IniConstants.Hard;
      } else if (suicidalRadioButton.IsChecked == true) {
        newIniData[appName][IniConstants.Difficulty] = IniConstants.Suicidal;
      } else if (hellOnEarthRadioButton.IsChecked == true) {
        newIniData[appName][IniConstants.Difficulty] = IniConstants.HellOnEarth;
      }

      /* Save the player count */
      newIniData[appName][IniConstants.MaxPlayers] = playerCountSlider.Value.ToString();

      /* Save the game length */
      if (shortRadioButton.IsChecked == true) {
        newIniData[appName][IniConstants.GameLength] = IniConstants.Short;
      } else if (mediumRadioButton.IsChecked == true) {
        newIniData[appName][IniConstants.GameLength] = IniConstants.Medium;
      } else if (longRadioButton.IsChecked == true) {
        newIniData[appName][IniConstants.GameLength] = IniConstants.Long;
      }

      /* Save the admin username and password */
      newIniData[appName][IniConstants.AdminUsername] = adminUsernameTextBox.Text;
      newIniData[appName][IniConstants.AdminPassword] = adminPasswordPasswordBox.Password;

      /* Save the extra options */
      newIniData[appName][IniConstants.ExtraOptions] = extraOptionsTextBox.Text;

      /* Save the dark style */
      newIniData[appName][IniConstants.DarkStyle] = darkStyleCheckBox.IsChecked == true ? IniConstants.True : IniConstants.False;
    }

    /* Validates the current configuration, which includes updating button states and status bar text */
    private void ValidateConfiguration() {
      validPath = true;

      /* If we're still initializing, stop now */
      if (initializing) return;

      /* Set our dark style (or not) */
      SetDarkStyle(darkStyleCheckBox.IsChecked);

      /* See if the directory exists */
      if (!Directory.Exists(kf2DirTextBox.Text)) validPath = false;

      /* Check for the KF2 server executable */
      if (!File.Exists(kf2DirTextBox.Text + Path.DirectorySeparatorChar + ServerExecutable)) validPath = false;

      /* Check for the Maps directory */
      if (!Directory.Exists(kf2DirTextBox.Text + Path.DirectorySeparatorChar + MapsDirectory)) validPath = false;

      /* If it's not valid, update our status bar text */
      if (!validPath) {
        /* Update our button states */
        commandLineButton.IsEnabled = false;
        startButton.IsEnabled = false;
        restartButton.IsEnabled = false;
        updateButton.IsEnabled = false;

        /* Update the status bar text */
        statusStatusBarItem.Content = Strings.BadServerDirectory;

        return;
      } else {
        /* Update our command line button state */
        commandLineButton.IsEnabled = true;

        /* Update our list of maps */
        if (mapsComboBox.Items.Count == 0) LoadMaps();
      }

      /* See if the user has selected a map */
      if (mapsComboBox.SelectedIndex < 0) {
        statusStatusBarItem.Content = Strings.SelectAMap;
        return;
      }

      /* Save the configuration */
      SaveConfiguration();

      /* See if the server is running */
      if (serverProcess != null && !serverProcess.HasExited) {
        /* Update our button states */
        startButton.IsEnabled = false;
        stopButton.IsEnabled = true;
        restartButton.IsEnabled = true;
        updateButton.IsEnabled = false;

        /* Update the status bar text */
        statusStatusBarItem.Content = Strings.ServerIsRunning;
      } else {
        /* Update our button states */
        startButton.IsEnabled = true;
        stopButton.IsEnabled = false;
        restartButton.IsEnabled = false;
        updateButton.IsEnabled = true;

        /* Update the status bar text */
        statusStatusBarItem.Content = Strings.ServerIsStopped;
      }
    }

    /* Load the available maps and update the combo box */
    private void LoadMaps() {
      ComboBoxItem oldMap = (ComboBoxItem) mapsComboBox.SelectedItem;

      /* Clear the list */
      mapsComboBox.Items.Clear();

      /* If the path is invalid, clear the list */
      if (!validPath) {
        return;
      }

      /* Add a Random Map entry */
      mapsComboBox.Items.Add(new ComboBoxItem());
      ((ComboBoxItem) mapsComboBox.Items[0]).Content = Strings.RandomMap;
      ((ComboBoxItem) mapsComboBox.Items[0]).FontWeight = FontWeights.Bold;

      /* Loop through each of the maps available and add it to the list */
      foreach (string map in Directory.GetFiles(kf2DirTextBox.Text + Path.DirectorySeparatorChar + MapsDirectory, MapsFilter, SearchOption.AllDirectories)) {
        mapsComboBox.Items.Add(new ComboBoxItem());
        ((ComboBoxItem) mapsComboBox.Items[mapsComboBox.Items.Count - 1]).Content = Path.GetFileNameWithoutExtension(map);
      }

      /* If the user had a map selected before, select it again */
      if (oldMap != null) {
        mapsComboBox.Text = oldMap.Content.ToString();
      }
    }

    /* Builds the command line required to start the server */
    private string BuildCommandLine() {
      StringBuilder commandLine = new StringBuilder();

      /* Star with the KF2 server directory */
      commandLine.Append(kf2DirTextBox.Text);
      if (!kf2DirTextBox.Text.EndsWith(Path.DirectorySeparatorChar.ToString())) commandLine.Append(Path.DirectorySeparatorChar);
      commandLine.Append(ServerExecutable);
      commandLine.Append(" ");

      /* Build the map (this must be the first option) */
      if (mapsComboBox.SelectedIndex == 0) {
        /* Pick a random map */
        commandLine.Append(((ComboBoxItem) mapsComboBox.Items[new Random().Next(1, mapsComboBox.Items.Count - 1)]).Content.ToString());
      } else if (mapsComboBox.SelectedIndex > 0) {
        commandLine.Append(mapsComboBox.Text);
      }

      /* Build the game mode */
      if (endlessRadioButton.IsChecked == true) {
        commandLine.Append(CommandEndlessMode);
      } else if (weeklyRadioButton.IsChecked == true) {
        commandLine.Append(CommandWeeklyMode);
      } else if (versusRadioButton.IsChecked == true) {
        commandLine.Append(CommandVersusMode);
      }

      /* Build the difficulty */
      commandLine.Append(CommandDifficulty);
      if (normalRadioButton.IsChecked == true) {
        commandLine.Append("0");
      } else if (hardRadioButton.IsChecked == true) {
        commandLine.Append("1");
      } else if (suicidalRadioButton.IsChecked == true) {
        commandLine.Append("2");
      } else if (hellOnEarthRadioButton.IsChecked == true) {
        commandLine.Append("3");
      }

      /* Build the max players */
      commandLine.Append(CommandMaxPlayers);
      commandLine.Append(playerCountSlider.Value.ToString());

      /* Build the game length */
      commandLine.Append(CommandGameLength);
      if (shortRadioButton.IsChecked == true) {
        commandLine.Append("0");
      } else if (mediumRadioButton.IsChecked == true) {
        commandLine.Append("1");
      } else if (longRadioButton.IsChecked == true) {
        commandLine.Append("2");
      }

      /* Build the admin username and password */
      commandLine.Append(CommandAdminName);
      commandLine.Append(adminUsernameTextBox.Text);
      commandLine.Append(CommandAdminPassword);
      commandLine.Append(adminPasswordPasswordBox.Password);

      /* Build the extra options */
      commandLine.Append(extraOptionsTextBox.Text);

      return commandLine.ToString();
    }

    /* Starts the server */
    private void StartServer() {
      string commandLine;
      string[] commandLineTokens;

      serverProcess = new Process();

      /* Build our command line */
      commandLine = BuildCommandLine();
      commandLineTokens = commandLine.Split(' ');

      /* Set up for executing the server */
      serverProcess.StartInfo.UseShellExecute = false;
      serverProcess.StartInfo.FileName = commandLineTokens[0];
      serverProcess.StartInfo.Arguments = commandLineTokens[1];
      serverProcess.EnableRaisingEvents = true;
      serverProcess.Exited += ServerProcess_Exited;

      /* Kick off the process */
      if (serverProcess.Start()) {
        /* Update window elements appropriately */
        ValidateConfiguration();
      }
    }

    /* Stops the server */
    private void StopServer() {
      /* Make sure the server is running */
      if (serverProcess != null && !serverProcess.HasExited) {
        /* This is horrible, but works well. Source: https://stackoverflow.com/a/15281070. */
        if (AttachConsole((uint) serverProcess.Id)) {
          SetConsoleCtrlHandler(null, true);
          GenerateConsoleCtrlEvent(CtrlTypes.CTRL_C_EVENT, 0);
          FreeConsole();
          serverProcess.WaitForExit(2000);
          SetConsoleCtrlHandler(null, false);
        }
      }
    }

    /* Enables or disables the dark style */
    private void SetDarkStyle(bool? darkStyle) {
      /* Don't do anything if we're initializing */
      if (initializing) return;

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

      /* Save our configuration */
      SaveConfiguration();
    }

    /* The server process terminated */
    private void ServerProcess_Exited(object sender, EventArgs e) => Dispatcher.BeginInvoke(new ThreadStart(() => ValidateConfiguration()));

    /* Server directory text box changed */
    private void Kf2DirTextBox_TextChanged(object sender, TextChangedEventArgs e) => ValidateConfiguration();

    /* The user clicked the KF2 server directory browse button */
    private void Kf2DirBrowseButton_Click(object sender, RoutedEventArgs e) {
      /* Show a folder picker */
      using (CommonOpenFileDialog commonOpenFileDialog = new CommonOpenFileDialog() { IsFolderPicker = true }) {
        CommonFileDialogResult dialogResult = commonOpenFileDialog.ShowDialog();

        if (dialogResult == CommonFileDialogResult.Ok && !string.IsNullOrWhiteSpace(commonOpenFileDialog.FileName)) {
          kf2DirTextBox.Text = commonOpenFileDialog.FileName;
        }
      }
    }

    /* The user checked the Survival radio button */
    private void SurvivalRadioButton_Checked(object sender, RoutedEventArgs e) => ValidateConfiguration();

    /* The user checked the Endless radio button */
    private void EndlessRadioButton_Checked(object sender, RoutedEventArgs e) => ValidateConfiguration();

    /* The user checked the Weekly Outbreaks radio button */
    private void WeeklyRadioButton_Checked(object sender, RoutedEventArgs e) => ValidateConfiguration();

    /* The user checked the Versus Survival radio button */
    private void VersusRadioButton_Checked(object sender, RoutedEventArgs e) => ValidateConfiguration();

    /* The user selected a map */
    private void MapsComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) => ValidateConfiguration();

    /* The user is refreshing the map list */
    private void MapRefreshButton_Click(object sender, RoutedEventArgs e) => LoadMaps();

    /* The user checked the Normal radio button */
    private void NormalRadioButton_Checked(object sender, RoutedEventArgs e) => ValidateConfiguration();

    /* The user checked the Hard radio button */
    private void HardRadioButton_Checked(object sender, RoutedEventArgs e) => ValidateConfiguration();

    /* The user checked the Suicidal radio button */
    private void SuicidalRadioButton_Checked(object sender, RoutedEventArgs e) => ValidateConfiguration();

    /* The user checked the Hell on Earth radio button */
    private void HellOnEarthRadioButton_Checked(object sender, RoutedEventArgs e) => ValidateConfiguration();

    /* The user changed the player count */
    private void PlayerCountSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) {
      /* Validate the configuration */
      ValidateConfiguration();

      /* Update our tooltip text to reflect the selected value */
      playerCountSlider.ToolTip = playerCountSlider.Value.ToString();
    }

    /* The user checked the Short radio button */
    private void ShortRadioButton_Checked(object sender, RoutedEventArgs e) => ValidateConfiguration();

    /* The user checked the Medium radio button */
    private void MediumRadioButton_Checked(object sender, RoutedEventArgs e) => ValidateConfiguration();

    /* The user checked the Long radio button */
    private void LongRadioButton_Checked(object sender, RoutedEventArgs e) => ValidateConfiguration();

    /* The user changed the admin username */
    private void AdminUsernameTextBox_LostFocus(object sender, RoutedEventArgs e) => ValidateConfiguration();

    /* The user changed the admin password */
    private void AdminPasswordPasswordBox_LostFocus(object sender, RoutedEventArgs e) => ValidateConfiguration();

    /* The user changed the text in the extra options text box */
    private void ExtraOptionsTextBox_TextChanged(object sender, RoutedEventArgs e) {
      extraOptionsTextBox.Text = extraOptionsTextBox.Text.Replace(" ", string.Empty);
    }

    /* The user changed the extra options */
    private void ExtraOptionsTextBox_LostFocus(object sender, RoutedEventArgs e) => ValidateConfiguration();

    /* The user wants to see the command line */
    private void CommandLineButton_Click(object sender, RoutedEventArgs e) => new CommandLineWindow(Strings.CommandLineIntro, BuildCommandLine(), mapsComboBox.SelectedIndex == 0 ? Strings.CommandLineRandom : string.Empty, darkStyleCheckBox.IsChecked).ShowDialog();

    /* The user wants to start the server */
    private void StartButton_Click(object sender, RoutedEventArgs e) => StartServer();

    /* The user wants to stop the server */
    private void StopButton_Click(object sender, RoutedEventArgs e) => StopServer();

    /* The user wants to restart the server */
    private void RestartButton_Click(object sender, RoutedEventArgs e) {
      StopServer();
      StartServer();
    }

    /* The user wants to update the server */
    private void UpdateButton_Click(object sender, RoutedEventArgs e) => new UpdateWindow(appName, kf2DirTextBox.Text, ref iniData, ref newIniData, darkStyleCheckBox.IsChecked).ShowDialog();

    /* The user wants to close the window */
    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    /* The user wants to toggle dark style */
    private void DarkStyleCheckBox_Checked(object sender, RoutedEventArgs e) => SetDarkStyle(darkStyleCheckBox.IsChecked);

    /* The window is closing */
    private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e) {
      /* Take care of external changes in the update window */
      UpdateWindow.ExternalSaveConfiguration(appName, ref iniData, ref newIniData);

      /* Write our settings to our .ini file */
      iniParser.WriteFile(iniFile, newIniData);
    }

    /* A text box got focus */
    private void SelectAllGotFocus(object sender, RoutedEventArgs e) {
      /* Select all text */
      ((TextBox) sender).SelectAll();
      e.Handled = true;
    }

    /* A text box got mouse capture */
    private void SelectAllGotMouseCapture(object sender, MouseEventArgs e) {
      /* Select all text */
      ((TextBox) sender).SelectAll();
      e.Handled = true;
    }

    /* A password box got focus */
    private void SelectAllPasswordGotFocus(object sender, RoutedEventArgs e) {
      /* Select all text */
      ((PasswordBox) sender).SelectAll();
      e.Handled = true;
    }

    /* A password box got mouse capture */
    private void SelectAllPasswordGotMouseCapture(object sender, MouseEventArgs e) {
      /* Select all text */
      ((PasswordBox) sender).SelectAll();
      e.Handled = true;
    }
  }
}
