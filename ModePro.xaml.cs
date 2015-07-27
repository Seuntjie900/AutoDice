﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shell;
using AutoDice.Sites;
using MahApps.Metro;
using MahApps.Metro.Controls.Dialogs;
using Microsoft.Win32;
using Sparrow.Chart;

namespace AutoDice
{
    /// <summary>
    /// Lógica de interacción para AdvancedMode.xaml
    /// </summary>
    public partial class ModePro
    {
        #region Variables
        // Variables from Initial Settings
        private double _NumStartingBet, _NumStartingChange;
        private string _RollOverUnder;

        // Variables from Martingale - On loss
        private double _NumMultiplierLoss, _NumMaxMultipliesLoss, _NumAfterLoss, _NumMultiplierAfterLoss, _NumAfterXLossesInRowChangeBet, _NumAfterXLossesInRowChangeBetNumber, _NumAfterXLossesInRowChangeChance, _NumAfterXLossesInRowChangeChanceNumber;
        private bool _RadMaxLoss, _RadVariableLoss, _RadConstrantLoss, _RadChangeOnceLoss, _ChkMultiplyOnlyOneTimeLoss;
        private bool _ChkAfterLossesInRowChangeBet, _ChkAfterLossesInRowChangeChance, _ChkReturnBaseBetAfterFirstLoss;

        // Variables from Martingale - On won
        private double _NumMultiplierWon, _NumMaxMultipliesWon, _NumAfterWon, _NumMultiplierAfterWon, _NumAfterXWonsInRowChangeBet, _NumAfterXWonsInRowChangeBetNumber, _NumAfterXWonsInRowChangeChance, _NumAfterXWonsInRowChangeChanceNumber;
        private bool _RadMaxWon, _RadVariableWon, _RadConstrantWon, _RadChangeOnceWon;
        private bool _ChkAfterWonsInRowChangeBet, _ChkAfterWonsInRowChangeChance, _ChkReturnBaseBetAfterFirstWon = true;

        // Variables for Fibonacci - On loss
        private bool _ChkFibonacciLossIncrease = true, _ChkFibonacciLossRestart = false, _ChkFibonacciLossStop = false;
        private int _NumFibonacciIncrementLoss = 1;

        // Variables for Fibonacci - On won
        private bool _ChkFibonacciWonIncrease = false, _ChkFibonacciWonRestart = true, _ChkFibonacciWonStop = false;
        private int _NumFibonacciIncrementWon = -1;
        // Other Fibonacci Variables
        private bool _ChkFibonacciWhenLevel, _ChkFibonacciWhenLevelReset, _ChkFibonacciWhenLevelStop, _fibonacci;
        private int _NumFibonacciWhenLevel = 10, FibonacciLevel = 0;

        // Variables for Constructor
        private readonly DiceSite _currentSite;
        private readonly string _username;

        // Winstreak, Losingstreak and Max Winning Streak and Max Losing Streak
        private int _WinStreak, _LossStreak, _MaxWinStreak, _MaxLossStreak;

        // Variables for Stop Conditions
        private bool _ChkBalanceLimit, _ChkBalanceLowerLimit, _ChkStopAfterBTCLoss,
            _ChkStopAfterLossesInRow, _ChkResetAfterBTCLoss, _ChkResetAfterLossesInRow,
            _ChkStopAfterBTCProfit, _ChkStopAfterWonsInRow, _ChkResetAfterBTCProfit, _ChkResetAfterWonsInRow;
        private double _NumBalanceLimit, _NumBalanceLowerLimit, _NumStopBTCLoss,
            _NumResetBTCLoss, _NumStopBTCProfit, _NumResetBTCProfit,
            _NumStopLosesInRow, _NumResetLosesInRow, _NumStopWonsInRow, _NumResetWonsInRow;

        // Variables for measuring the speed of bets
        private DateTime _initialDateTime;
        private double _delay = 3;

        // Variables for Tipping
        private string _Payee;
        private double _AmountTip;

        // Other variables
        private readonly List<GenericDataGrid> _datosRoll = new List<GenericDataGrid>();
        private readonly BackgroundWorker _rollWorker = new BackgroundWorker();
        readonly BackgroundWorker _tipWorker = new BackgroundWorker();
        private GenericBalance _tipData;
        readonly IniParser _parser = new IniParser("config.ini");
        private bool _HasCleanedGraph;

        private int _WonsCounter, _LostCounter;
        private double _BiggestWon, _BiggestLost, _BiggestBet;

        private double _balance, _initialBalance;
        private bool _martingale, _manuallyStopped;

        private bool _DoOneRoll, _StopOnWon, _AutoScroll, _ShowWons, _ShowLosses;

        // Variables for Martingale Logic
        private double _CurrentMultiplier, _CurrentBet, _CurrentChance;

        // Variables for Current roll data
        private GenericRoll _roll;

        private double totalProfit;

        #endregion

        #region Constructor

        /// <summary>
        /// Constructor for General sites
        /// </summary>
        /// <param name="site">Current site</param>
        /// <param name="username">Current username</param>
        /// <param name="balance">Current balance (in BTC)</param>
        public ModePro(DiceSite site, string username, double balance)
        {
            InitializeComponent();
            _currentSite = site;
            _username = username;
            _balance = balance;

            InitBackgroundWorkers();
            InitThemeSettings();

            Title = string.Format("AutoDice Pro: {0}", _username);
        }

        #endregion

        #region RollWorker Methods

        #region DoWork
        private void RollWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            _CurrentBet = _NumStartingBet;
            _CurrentChance = _NumStartingChange;
            _CurrentMultiplier = _NumMultiplierLoss;
            _initialBalance = _balance;
            _manuallyStopped = false;
            _WinStreak = 0;
            _LossStreak = 0;
            var counter = 0;
            _initialDateTime = DateTime.Now;
            while (!_manuallyStopped)
            {
                if ((sender as BackgroundWorker).CancellationPending)
                {
                    e.Cancel = true;
                    break;
                }
                //var timelapse = Stopwatch.StartNew();
                _roll = RequestBet();
                if (_roll.status)
                {
                    totalProfit += _roll.data.profit;
                    if (_DoOneRoll)
                    {
                        _manuallyStopped = true;
                    }
                    counter++;
                    _balance = _roll.balance;
                    if (_roll.data != null && _roll.data.status.Equals("WIN"))
                    {
                        if (_StopOnWon)
                        {
                            _manuallyStopped = true;
                        }
                        _WonsCounter++;
                        _WinStreak++;
                        _LossStreak = 0;
                        if (_WinStreak > _MaxWinStreak)
                        {
                            _MaxWinStreak = _WinStreak;
                        }
                        if (_BiggestBet < _roll.data.amount)
                        {
                            _BiggestBet = _roll.data.amount;
                        }
                        if (_BiggestWon < _roll.data.payout)
                        {
                            _BiggestWon = _roll.data.payout;
                        }
                        if (_martingale)
                        {
                            Martingale(true);
                        }
                        else if (_fibonacci)
                        {
                            Fibonacci(true);
                        }
                    }
                    else if (_roll.data != null && _roll.data.status.Equals("LOSS"))
                    {
                        _LostCounter++;
                        _LossStreak++;
                        _WinStreak = 0;
                        if (_LossStreak > _MaxLossStreak)
                        {
                            _MaxLossStreak = _LossStreak;
                        }
                        if (_BiggestBet < _roll.data.amount)
                        {
                            _BiggestBet = _roll.data.amount;
                        }
                        if (_BiggestLost < _roll.data.amount)
                        {
                            _BiggestLost = _roll.data.amount;
                        }
                        if (_martingale)
                        {
                            Martingale(false);
                        }
                        else if (_fibonacci)
                        {
                            Fibonacci(false);
                        }
                    }
                    CheckStopConditions();
                }

                //timelapse.Stop();

                //if (timelapse.ElapsedMilliseconds < (1000 / _delay))
                //{
                //    Thread.Sleep((int)(1000 / _delay) - (int)(timelapse.ElapsedMilliseconds));
                //}

                (sender as BackgroundWorker).ReportProgress(counter);
            }
        }
        #endregion

        #region ProgressChanged
        private void RollWorker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            UpdateLabels();
            #region Update Speed label
            var betsPerSecond = Math.Round((e.ProgressPercentage / (DateTime.Now - _initialDateTime).TotalSeconds), 2, MidpointRounding.AwayFromZero);
            lblSpeed.Content = string.Format("{0} Bet{1}/s", betsPerSecond, betsPerSecond <= 1 ? string.Empty : "s");

            #endregion
            #region Datagrid
            if (_roll.status && _roll.data != null)
            {
                var data = new GenericDataGrid
                {
                    status = _roll.data.status,
                    amount = _roll.data.amount.ToString("0.00000000", CultureInfo.InvariantCulture),
                    payout = _roll.data.payout.ToString("0.00000000", CultureInfo.InvariantCulture),
                    profit = _roll.data.profit.ToString("0.00000000", CultureInfo.InvariantCulture),
                    result = _roll.data.result,
                    bet = _roll.data.bet,
                    chance = string.Format("{0}%", _roll.data.chance),
                    id = _roll.data.id,
                    hour = DateTime.Now.ToString("HH:mm:ss", DateTimeFormatInfo.InvariantInfo)
                };
                if ((_ShowWons && _roll.data.status.Equals("WIN")) ||
                    (_ShowLosses && _roll.data.status.Equals("LOSS")))
                {
                    var selected = GridBets.SelectedIndex;
                    _datosRoll.Add(data);
                    GridBets.Items.Refresh();
                    GridBets.SelectedIndex = selected;
                }
            }
            #endregion
            #region AutoScroll
            if (_AutoScroll && GridBets.Items.Count > 0)
            {
                GridBets.ScrollIntoView(GridBets.Items[GridBets.Items.Count - 1]);
            }
            #endregion
            #region Status Update

            if (_roll.status && _roll.data != null)
            {
                LblStatus.Content = string.Format("Bet {0}: {1}", e.ProgressPercentage, _roll.data.status);
                LblStatus.Foreground = _roll.data.status.Equals("WIN") ? Brushes.Green : Brushes.Red;
            }
            else
            {
                LblStatus.Content = string.Format("Bet {0}: {1} [Retrying]", e.ProgressPercentage + 1, _roll.error);
                LblStatus.Foreground = Brushes.Orange;
            }
            #endregion
            #region Update selected item in Fibonacci
            if (_fibonacci)
            {
                lstFibonacci.SelectedIndex = FibonacciLevel;
            }
            #endregion
            #region Graph Update
            if (TabGraph.IsSelected)
            {
                _HasCleanedGraph = false;
                Profit.Points.Add(new DoublePoint{Data = e.ProgressPercentage, Value = totalProfit});
                if (Profit.Points.Count > 100)
                {
                    Profit.Points.RemoveAt(0);
                }

                lblAverageProfit.Content = string.Format("Average Profit: {0} / 10 minutes", ((totalProfit / e.ProgressPercentage) * betsPerSecond * 600).ToString("0.00000000"));
            }
            else if (!_HasCleanedGraph)
            {
                RestartGraph();
                _HasCleanedGraph = true;
            }
            
            #endregion
        }
        #endregion

        #region RunWorkerCompleted
        private void RollWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            TaskbarItemInfo.ProgressState = TaskbarItemProgressState.None;
            btnNormalMode.IsEnabled = true;
            BtnStartStrategie.IsEnabled = true;
            BtnStartOneRoll.IsEnabled = true;
            BtnStop.IsEnabled = false;
            BtnStopOnWon.IsEnabled = false;
            BtnCheckMartingale.IsEnabled = true;
            ProgressBetting.IsIndeterminate = false;
            if (_martingale)
            {
                TabFibonacci.IsEnabled = true;
            }
            else if (_fibonacci)
            {
                TabMartingale.IsEnabled = true;
            }
            btnLoadSave.IsEnabled = true;
            LblStatus.Content = LblStatus.Content.ToString().Replace(" [Retrying]", String.Empty);
            LblStatus.Content = string.Format("{0}{1} Bets Stopped.", LblStatus.Content, LblStatus.Content.ToString().Contains(".") ? string.Empty : ".");
        }
        #endregion

        #endregion

        #region TipWorker Methods

        #region DoWork
        private void tipWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            _tipData = null;
            _tipData = _currentSite.Tip(_Payee, _AmountTip);
            if (_tipData.status)
            {
                _balance = _tipData.balance;
            }
        }

        #endregion

        #region RunWorkerCompleted
        private void tipWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            UpdateLabels();
            FlyoutProcessExecution.IsOpen = true;
            ProgressTip.IsIndeterminate = false;
            ProgressTip.Visibility = Visibility.Hidden;

            if (_tipData.status)
            {
                lblStatusTip.Content = string.Format("Tip sent to {0}.", _Payee);
                LblBalance.Content = _balance.ToString("0.00000000 BTC").Replace(",", ".");
            }
            else
            {
                lblStatusTip.Content = _tipData.error;
            }

            #region Generates a delay for autoclosing the Flyout
            using (var bG = new BackgroundWorker())
            {
                bG.DoWork += (s, j) =>
                {
                    var aux = Stopwatch.StartNew();
                    while (true) { if (aux.Elapsed.Seconds >= 5) { aux.Stop(); break; } }
                };
                bG.RunWorkerCompleted += (s, j) => { FlyoutProcessExecution.IsOpen = false; };

                bG.RunWorkerAsync();
            }

            #endregion
        }

        #endregion

        #endregion

        #region Methods

        #region Buttons Functionallities
        private void BtnStartOneRoll_Click(object sender, RoutedEventArgs e)
        {
            _NumStartingBet = (double)NumStartingBet.Value;
            _NumStartingChange = (double)NumStartingChance.Value;
            _NumMultiplierLoss = (double)NumMultiplierLoss.Value;
            _DoOneRoll = true;
            _StopOnWon = false;
            BtnStartStrategie.IsEnabled = false;
            BtnStartOneRoll.IsEnabled = false;
            BtnStop.IsEnabled = false;
            BtnStopOnWon.IsEnabled = false;
            BtnCheckMartingale.IsEnabled = false;
            _rollWorker.RunWorkerAsync();
            ProgressBetting.IsIndeterminate = true;
        }
        private void BtnStartStrategie_Click(object sender, RoutedEventArgs e)
        {
            totalProfit = 0;
            _NumStartingBet = (double)NumStartingBet.Value;
            _NumStartingChange = (double)NumStartingChance.Value;
            _NumMultiplierLoss = (double)NumMultiplierLoss.Value;
            _DoOneRoll = false;
            _StopOnWon = false;
            BtnStartStrategie.IsEnabled = false;
            BtnStartOneRoll.IsEnabled = false;
            BtnCheckMartingale.IsEnabled = false;
            BtnStop.IsEnabled = true;
            BtnStopOnWon.IsEnabled = true;
            if (_martingale)
            {
                TabFibonacci.IsEnabled = false;
            }
            else if (_fibonacci)
            {
                TabMartingale.IsEnabled = false;
            }
            btnLoadSave.IsEnabled = false;
            RestartGraph();
            
            TaskbarItemInfo.ProgressState = TaskbarItemProgressState.Indeterminate;
            _rollWorker.RunWorkerAsync();
            btnNormalMode.IsEnabled = false;
            ProgressBetting.IsIndeterminate = true;
        }
        private void BtnStop_Click(object sender, RoutedEventArgs e)
        {
            BtnStop.IsEnabled = false;
            BtnStopOnWon.IsEnabled = false;
            _rollWorker.CancelAsync();
        }
        private void BtnStopOnWon_Click(object sender, RoutedEventArgs e)
        {
            _StopOnWon = true;
            BtnStopOnWon.IsEnabled = false;
        }

        #endregion

        #region Martingale

        #region Martingale Logic
        private void Martingale(bool Won)
        {
            if (Won)
            {
                if (_RadMaxWon && _WinStreak >= _NumMaxMultipliesWon)
                {
                    _CurrentMultiplier = 1;
                }
                else if (_RadMaxWon && _WinStreak < _NumMaxMultipliesWon)
                {
                    _CurrentMultiplier = _NumMultiplierWon;
                }
                else if (_RadVariableWon && _WinStreak >= _NumAfterWon && _WinStreak % (int)_NumAfterWon == 1)
                {
                    _CurrentMultiplier *= _NumMultiplierAfterWon;
                }
                else if (_RadChangeOnceWon && _WinStreak == (int)_NumAfterWon)
                {
                    _CurrentMultiplier *= _NumMultiplierAfterWon;
                }
                else if (_RadChangeOnceWon && _WinStreak < (int)_NumAfterWon)
                {
                    _CurrentMultiplier = _NumMultiplierWon;
                }
                else if (_RadConstrantWon)
                {
                    _CurrentMultiplier = _NumMultiplierWon;
                }
                _CurrentBet *= _CurrentMultiplier;
                if (_CurrentBet < 0.00000000)
                {
                    _CurrentBet = 0.00000001;
                }
                if (_WinStreak == 1)
                {
                    _CurrentChance = _NumStartingChange;
                    if (_ChkReturnBaseBetAfterFirstWon)
                    {
                        _CurrentBet = _NumStartingBet;
                        _CurrentMultiplier = _NumMultiplierLoss;
                    }
                }
                if (_ChkAfterWonsInRowChangeBet && (_WinStreak == (int)_NumAfterXWonsInRowChangeBet))
                {
                    _CurrentBet = _NumAfterXWonsInRowChangeBetNumber;
                }
                if (_ChkAfterWonsInRowChangeChance && (_WinStreak == (int)_NumAfterXWonsInRowChangeChance))
                {
                    _CurrentChance = _NumAfterXWonsInRowChangeChanceNumber;
                }
            }
            else
            {
                if (_RadMaxLoss && _LossStreak >= _NumMaxMultipliesLoss)
                {
                    _CurrentMultiplier = 1;
                }
                else if (_RadMaxLoss && _LossStreak < _NumMaxMultipliesLoss)
                {
                    _CurrentMultiplier = _NumMultiplierLoss;
                }
                else if (_RadVariableLoss && _LossStreak >= _NumAfterLoss && _LossStreak % (int)_NumAfterLoss == 1)
                {
                    _CurrentMultiplier *= _NumMultiplierAfterLoss;
                }
                else if (_RadChangeOnceLoss && _LossStreak == (int)_NumAfterLoss)
                {
                    _CurrentMultiplier *= _NumMultiplierAfterLoss;
                }
                else if (_RadChangeOnceLoss && _LossStreak < (int)_NumAfterLoss)
                {
                    _CurrentMultiplier = _NumMultiplierLoss;
                }
                else if (_RadConstrantLoss)
                {
                    _CurrentMultiplier = _NumMultiplierLoss;
                }
                _CurrentBet *= _CurrentMultiplier;
                if (_ChkMultiplyOnlyOneTimeLoss && _RadVariableLoss && _LossStreak > 0 && _LossStreak % (int)_NumAfterLoss == 1)
                {
                    _CurrentMultiplier = _NumMultiplierLoss;
                }
                if (_CurrentBet < 0.00000000)
                {
                    _CurrentBet = 0.00000001;
                }
                if (_LossStreak == 1)
                {
                    _CurrentChance = _NumStartingChange;
                    if (_ChkReturnBaseBetAfterFirstLoss)
                    {
                        _CurrentBet = _NumStartingBet;
                        _CurrentMultiplier = _NumMultiplierWon;
                    }
                }
                if (_ChkAfterLossesInRowChangeBet && (_LossStreak == (int)_NumAfterXLossesInRowChangeBet))
                {
                    _CurrentBet = _NumAfterXLossesInRowChangeBetNumber;
                }
                if (_ChkAfterLossesInRowChangeChance && (_LossStreak == (int)_NumAfterXLossesInRowChangeChance))
                {
                    _CurrentChance = _NumAfterXLossesInRowChangeChanceNumber;
                }
            }
        }

        #endregion

        #region Martingale Check Current Settings
        private void CheckMartingale()
        {
            _CurrentMultiplier = _NumMultiplierLoss;
            _CurrentBet = _NumStartingBet;
            _LossStreak = 0;
            var aux = _balance;
            var contador = 0;
            while (aux > _CurrentBet)
            {
                _LossStreak++;
                aux -= _CurrentBet;
                Martingale(false);
                contador++;
            }
            _LossStreak = 0;
            _CurrentMultiplier = _NumMultiplierLoss;
            _CurrentBet = _NumStartingBet;
            ShowNormalDialog("Martingale Check", string.Format("With your current balance, you can hold on {0} loses in a row. If you lose at bet number {1}, your remaining balance will be {2} BTC.", contador - 1, contador, aux.ToString("0.00000000").Replace(",", ".")));
        }
        private void BtnCheckMartingale_Click(object sender, RoutedEventArgs e)
        {
            CheckMartingale();
        }

        #endregion

        #endregion

        #region Fibonacci

        #region Method to Populate Fibonacci
        private void PopulateFibonacci(double number)
        {
            double Previous = 0;
            var Current = number;
            lstFibonacci.Items.Clear();
            for (var i = 1; i <= 50; i++)
            {
                lstFibonacci.Items.Add(string.Format("{0}. {1}", i, ToServerString(Current, true)));
                var tmp = Current;
                Current += Previous;
                Previous = tmp;
            }
        }

        #endregion
        #region Fibonacci Logic
        private void Fibonacci(bool Won)
        {
            if (Won)
            {
                if (_ChkFibonacciWonIncrease)
                {
                    FibonacciLevel += _NumFibonacciIncrementWon;
                }
                else if (_ChkFibonacciWonRestart)
                {
                    FibonacciLevel = 0;
                }
                else if (_ChkFibonacciWonStop)
                {
                    FibonacciLevel = 0;
                    _manuallyStopped = true;
                }
            }
            else
            {
                if (_ChkFibonacciLossIncrease)
                {
                    FibonacciLevel += _NumFibonacciIncrementLoss;
                }
                else if (_ChkFibonacciLossRestart)
                {
                    FibonacciLevel = 0;
                }
                else if (_ChkFibonacciLossStop)
                {
                    FibonacciLevel = 0;
                    _manuallyStopped = true;
                }
            }
            if (FibonacciLevel < 0)
            {
                FibonacciLevel = 0;
            }
            else if (FibonacciLevel > 49)
            {
                FibonacciLevel = 49;
            }
            if (_ChkFibonacciWhenLevel && FibonacciLevel >= _NumFibonacciWhenLevel)
            {
                if (_ChkFibonacciWhenLevelReset)
                    FibonacciLevel = 0;
                else if (_ChkFibonacciWhenLevelStop)
                {
                    FibonacciLevel = 0;
                    _manuallyStopped = true;
                }
            }
            _CurrentBet = double.Parse(lstFibonacci.Items[FibonacciLevel].ToString().Substring(lstFibonacci.Items[FibonacciLevel].ToString().IndexOf(" ", StringComparison.Ordinal) + 1).Replace(",", "."), CultureInfo.InvariantCulture);
        }

        #endregion

        #endregion

        #region Simulation
        private void Simulate()
        {
            _CurrentMultiplier = _NumMultiplierLoss;
            _CurrentBet = _NumStartingBet;
            _LossStreak = 0;
            var aux = _balance;
            var contador = 0;
            while (aux > _CurrentBet)
            {
                _LossStreak++;
                aux -= _CurrentBet;
                Martingale(false);
                contador++;
            }
            _LossStreak = 0;
            _CurrentMultiplier = _NumMultiplierLoss;
            _CurrentBet = _NumStartingBet;
            ShowNormalDialog("Martingale Check", string.Format("With your current balance, you can hold on {0} loses in a row. If you lose at bet number {1}, your remaining balance will be {2} BTC.", contador - 1, contador, aux.ToString("0.00000000").Replace(",", ".")));

            while (true)
            {
                var number = new Random().NextDouble() * 99.99;
                break;
            }
        }
        #endregion

        #region Stop Conditions
        private void CheckStopConditions()
        {
            #region Balance Limits
            if (_ChkBalanceLimit && _NumBalanceLimit < _balance)
            {
                _manuallyStopped = true;
            }
            if (_ChkBalanceLowerLimit && _balance < _NumBalanceLowerLimit)
            {
                _manuallyStopped = true;
            }

            #endregion
            #region On Losses Conditions
            if (_ChkStopAfterBTCLoss && _initialBalance - _balance <= _NumStopBTCLoss)
            {
                _manuallyStopped = true;
            }
            if (_ChkStopAfterLossesInRow && _LossStreak >= _NumStopLosesInRow)
            {
                _manuallyStopped = true;
            }
            if (_ChkResetAfterBTCLoss && _initialBalance - _balance <= _NumResetBTCLoss)
            {
                _CurrentBet = _NumStartingBet;
                _CurrentMultiplier = _NumMultiplierWon;

            }
            if (_ChkResetAfterLossesInRow && _LossStreak >= _NumResetLosesInRow)
            {
                _CurrentBet = _NumStartingBet;
                _CurrentMultiplier = _NumMultiplierWon;
            }
            #endregion
            #region On Wons Conditions
            if (_ChkStopAfterBTCProfit && _balance - _initialBalance >= _NumStopBTCProfit)
            {
                _manuallyStopped = true;
            }
            if (_ChkStopAfterWonsInRow && _WinStreak >= _NumStopWonsInRow)
            {
                _manuallyStopped = true;
            }
            if (_ChkResetAfterBTCProfit && _balance - _initialBalance >= _NumResetBTCProfit)
            {
                _CurrentBet = _NumStartingBet;
                _CurrentMultiplier = _NumMultiplierWon;
            }
            if (_ChkResetAfterWonsInRow && _WinStreak >= _NumResetWonsInRow)
            {
                _CurrentBet = _NumStartingBet;
                _CurrentMultiplier = _NumMultiplierWon;
            }

            #endregion
        }

        #endregion

        #region Methods to avoid closing window if betting

        private bool avoidClosing = true;

        protected override void OnClosing(CancelEventArgs e)
        {
            if (_rollWorker.IsBusy && avoidClosing)
            {
                e.Cancel = true;
                ShowCloseMessage();
            }
            else
            {
                e.Cancel = false;
            }
        }
        private async void ShowCloseMessage()
        {
            var mySettings = new MetroDialogSettings()
            {
                AffirmativeButtonText = "Yes",
                NegativeButtonText = "No",
                ColorScheme = MetroDialogOptions.ColorScheme
            };

            var result = await this.ShowMessageAsync("Betting", "You are betting right now. ¿Are you sure do you want to quit?",
            MessageDialogStyle.AffirmativeAndNegative, mySettings);

            if (result != MessageDialogResult.Affirmative) return;
            avoidClosing = false;
            Close();
        }
        #endregion

        #region Initialize BackgroundWorkers
        private void InitBackgroundWorkers()
        {
            _rollWorker.WorkerReportsProgress = true;
            _rollWorker.WorkerSupportsCancellation = true;
            _rollWorker.DoWork += RollWorker_DoWork;
            _rollWorker.ProgressChanged += RollWorker_ProgressChanged;
            _rollWorker.RunWorkerCompleted += RollWorker_RunWorkerCompleted;

            _tipWorker.DoWork += tipWorker_DoWork;
            _tipWorker.RunWorkerCompleted += tipWorker_RunWorkerCompleted;
        }

        #endregion

        #region Window Loaded Method
        private void MetroWindow_Loaded(object sender, RoutedEventArgs e)
        {
            GridBets.ItemsSource = _datosRoll;
            UpdateLabels();
            PopulateFibonacci(_NumStartingBet);
            lblVersion.Content = string.Format("Version {0} by @sbarrenechea", Assembly.GetExecutingAssembly().GetName().Version.ToString().Remove(5));
            if (File.Exists("default.ini"))
            {
                LoadStratConfig("default.ini");
            }
            NumStartingChance.Maximum = _currentSite.MaxMultiplier;
            NumStartingChance.Minimum = _currentSite.MinMultiplier;
            btnTip.IsEnabled = _currentSite.CanTip;
            numAmountTip.Minimum = _currentSite.MinTipAmount;
            numAmountTip.Interval = _currentSite.TipAmountInterval;

            ShowNormalDialog("Beta release", "This mode is currently on beta. If you found any bug, please report that to me!");
        }

        #endregion

        #region Strat System Changer Method
        private void TabStrategies_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _martingale = TabMartingale.IsSelected;
            _fibonacci = TabFibonacci.IsSelected;
            if (TabMartingale.IsSelected)
            {
                BtnStartStrategie.Content = "Start Martingale";
            }
            else if (TabFibonacci.IsSelected)
            {
                BtnStartStrategie.Content = "Start Fibonacci";
            }
        }

        #endregion

        #region Values changed

        #region Initial Settings
        private void NumStartingBet_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double?> e)
        {
            try
            {
                _NumStartingBet = (double)NumStartingBet.Value;
                if (TabFibonacci.IsSelected)
                {
                    PopulateFibonacci(_NumStartingBet);
                }
            }
            catch
            {
                // ignored
            }
        }
        private void NumStartingChance_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double?> e)
        {
            try
            {
                NumStartingChance.Value = Math.Round((double)NumStartingChance.Value, 2);
                _NumStartingChange = (double)NumStartingChance.Value;
                _CurrentChance = _NumStartingChange;
                txtNextBetPayout.Text = string.Format("{0}x", CalculatePayout(_NumStartingChange));
                _delay = (double)NumDelay.Value;
                RadRollOver.Content = string.Format("Over {0}", (99.99 - _NumStartingChange).ToString("0.00").Replace(",", "."));
                RadRollUnder.Content = string.Format("Under {0}", _NumStartingChange.ToString("0.00").Replace(",", "."));
            }
            catch
            {
                // ignored
            }
        }
        private void RadRollOver_CheckedChanged(object sender, RoutedEventArgs e)
        {
            try
            {
                _RollOverUnder = (bool)RadRollOver.IsChecked ? "over" : "under";
            }
            catch
            {
                // ignored
            }
        }
        #endregion

        #region Martingale - On loss
        private void NumMultiplierLoss_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double?> e)
        {
            try
            {
                NumMultiplierLoss.Value = Math.Round((double)NumMultiplierLoss.Value, 2);
                _NumMultiplierLoss = (double)NumMultiplierLoss.Value;
            }
            catch
            {
                // ignored
            }
        }
        private void NumMaxMultipliesLoss_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double?> e)
        {
            try
            {
                _NumMaxMultipliesLoss = (double)NumMaxMultipliesLoss.Value;
            }
            catch
            {
                // ignored
            }
        }
        private void NumAfterLoss_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double?> e)
        {
            try
            {
                _NumAfterLoss = (double)NumAfterLoss.Value;
            }
            catch
            {
                // ignored
            }
        }
        private void NumMultiplierAfterLoss_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double?> e)
        {
            try
            {
                NumMultiplierAfterLoss.Value = Math.Round((double)NumMultiplierAfterLoss.Value, 2);
                _NumMultiplierAfterLoss = (double)NumMultiplierAfterLoss.Value;
            }
            catch
            {
                // ignored
            }
        }

        private void NumAfterXLossesInRowChangeBet_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double?> e)
        {
            try
            {
                _NumAfterXLossesInRowChangeBet = (double)NumAfterXLossesInRowChangeBet.Value;
            }
            catch
            {
                // ignored
            }
        }
        private void NumAfterXLossesInRowChangeBetNumber_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double?> e)
        {
            try
            {
                _NumAfterXLossesInRowChangeBetNumber = (double)NumAfterXLossesInRowChangeBetNumber.Value;
            }
            catch
            {
                // ignored
            }
        }
        private void NumAfterXLossesInRowChangeChance_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double?> e)
        {
            try
            {
                _NumAfterXLossesInRowChangeChance = (double)NumAfterXLossesInRowChangeChance.Value;
            }
            catch
            {
                // ignored
            }
        }
        private void NumAfterXLossesInRowChangeChanceNumber_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double?> e)
        {
            try
            {
                NumAfterXLossesInRowChangeChanceNumber.Value = Math.Round((double)NumAfterXLossesInRowChangeChanceNumber.Value, 2);
                _NumAfterXLossesInRowChangeChanceNumber = (double)NumAfterXLossesInRowChangeChanceNumber.Value;
            }
            catch
            {
                // ignored
            }
        }
        private void AllLossRadioButtons_CheckedChanged(object sender, RoutedEventArgs e)
        {
            try
            {
                _RadMaxLoss = (bool)RadMaxLoss.IsChecked;
                _RadVariableLoss = (bool)RadVariableLoss.IsChecked;
                _RadConstrantLoss = (bool)RadConstrantLoss.IsChecked;
                _RadChangeOnceLoss = (bool)RadChangeOnceLoss.IsChecked;

                #region Disable All things

                NumMaxMultipliesLoss.IsEnabled = false;
                NumAfterLoss.IsEnabled = false;
                NumMultiplierAfterLoss.IsEnabled = false;
                ChkMultiplyOnlyOneTimeLoss.IsEnabled = false;

                #endregion
                if (_RadMaxLoss)
                {
                    NumMaxMultipliesLoss.IsEnabled = true;
                }
                if (_RadVariableLoss || _RadChangeOnceLoss)
                {
                    NumAfterLoss.IsEnabled = true;
                    NumMultiplierAfterLoss.IsEnabled = true;
                }
                if (_RadVariableLoss)
                {
                    ChkMultiplyOnlyOneTimeLoss.IsEnabled = true;
                }
            }
            catch
            {
                // ignored
            }
        }
        private void AllLossCheckBox_CheckedChanged(object sender, RoutedEventArgs e)
        {
            _ChkAfterLossesInRowChangeBet = (bool)ChkAfterLossesInRowChangeBet.IsChecked;
            _ChkAfterLossesInRowChangeChance = (bool)ChkAfterLossesInRowChangeChance.IsChecked;
            _ChkReturnBaseBetAfterFirstLoss = (bool)ChkReturnBaseBetAfterFirstLoss.IsChecked;
            NumAfterXLossesInRowChangeBet.IsEnabled = _ChkAfterLossesInRowChangeBet;
            NumAfterXLossesInRowChangeBetNumber.IsEnabled = _ChkAfterLossesInRowChangeBet;
            NumAfterXLossesInRowChangeChance.IsEnabled = _ChkAfterLossesInRowChangeChance;
            NumAfterXLossesInRowChangeChanceNumber.IsEnabled = _ChkAfterLossesInRowChangeChance;
        }
        private void ChkMultiplyOnlyOneTimeLoss_CheckedChanged(object sender, RoutedEventArgs e)
        {
            try
            {
                _ChkMultiplyOnlyOneTimeLoss = (bool)ChkMultiplyOnlyOneTimeLoss.IsChecked;
            }
            catch
            {
                // ignored
            }
        }

        #endregion

        #region Martingale - On won
        private void NumMultiplierWon_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double?> e)
        {
            try
            {
                NumMultiplierWon.Value = Math.Round((double)NumMultiplierWon.Value, 2);
                _NumMultiplierWon = (double)NumMultiplierWon.Value;
                _CurrentMultiplier = _NumMultiplierWon;
            }
            catch
            {
                // ignored
            }
        }
        private void NumMaxMultipliesWon_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double?> e)
        {
            try
            {
                _NumMaxMultipliesWon = (double)NumMaxMultipliesWon.Value;
            }
            catch
            {
                // ignored
            }
        }
        private void NumAfterWon_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double?> e)
        {
            try
            {
                _NumAfterWon = (double)NumAfterWon.Value;
            }
            catch
            {
                // ignored
            }
        }
        private void NumMultiplierAfterWon_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double?> e)
        {
            try
            {
                NumMultiplierAfterWon.Value = Math.Round((double)NumMultiplierAfterWon.Value, 2);
                _NumMultiplierAfterWon = (double)NumMultiplierAfterWon.Value;
            }
            catch
            {
                // ignored
            }
        }
        private void NumAfterXWonsInRowChangeBet_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double?> e)
        {
            try
            {
                _NumAfterXWonsInRowChangeBet = (double)NumAfterXWonsInRowChangeBet.Value;
            }
            catch
            {
                // ignored
            }
        }
        private void NumAfterXWonsInRowChangeBetNumber_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double?> e)
        {
            try
            {
                _NumAfterXWonsInRowChangeBetNumber = (double)NumAfterXWonsInRowChangeBetNumber.Value;
            }
            catch
            {
                // ignored
            }
        }
        private void NumAfterXWonsInRowChangeChance_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double?> e)
        {
            try
            {
                _NumAfterXWonsInRowChangeChance = (double)NumAfterXWonsInRowChangeChance.Value;
            }
            catch
            {
                // ignored
            }
        }
        private void NumAfterXWonsInRowChangeChanceNumber_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double?> e)
        {
            try
            {
                NumAfterXWonsInRowChangeChanceNumber.Value = Math.Round((double)NumAfterXWonsInRowChangeChanceNumber.Value, 2);
                _NumAfterXWonsInRowChangeChanceNumber = (double)NumAfterXWonsInRowChangeChanceNumber.Value;
            }
            catch
            {
                // ignored
            }
        }
        private void AllWonRadioButtons_CheckedChanged(object sender, RoutedEventArgs e)
        {
            try
            {
                _RadMaxWon = (bool)RadMaxWon.IsChecked;
                _RadVariableWon = (bool)RadVariableWon.IsChecked;
                _RadConstrantWon = (bool)RadConstrantWon.IsChecked;
                _RadChangeOnceWon = (bool)RadChangeOnceWon.IsChecked;

                #region Disable All things

                NumMaxMultipliesWon.IsEnabled = false;
                NumAfterWon.IsEnabled = false;
                NumMultiplierAfterWon.IsEnabled = false;

                #endregion

                if (_RadMaxWon)
                {
                    NumMaxMultipliesWon.IsEnabled = true;
                }
                if (_RadVariableWon || _RadChangeOnceWon)
                {
                    NumAfterWon.IsEnabled = true;
                    NumMultiplierAfterWon.IsEnabled = true;
                }
            }
            catch
            {
                // ignored
            }
        }
        private void AllWonCheckBox_CheckedChanged(object sender, RoutedEventArgs e)
        {
            try
            {
                _ChkAfterWonsInRowChangeBet = (bool)ChkAfterWonsInRowChangeBet.IsChecked;
                _ChkAfterWonsInRowChangeChance = (bool)ChkAfterWonsInRowChangeChance.IsChecked;
                _ChkReturnBaseBetAfterFirstWon = (bool)ChkReturnBaseBetAfterFirstWon.IsChecked;
                NumAfterXWonsInRowChangeBet.IsEnabled = _ChkAfterWonsInRowChangeBet;
                NumAfterXWonsInRowChangeBetNumber.IsEnabled = _ChkAfterWonsInRowChangeBet;
                NumAfterXWonsInRowChangeChance.IsEnabled = _ChkAfterWonsInRowChangeChance;
                NumAfterXWonsInRowChangeChanceNumber.IsEnabled = _ChkAfterWonsInRowChangeChance;
            }
            catch
            {
                // ignored
            }
        }
        #endregion

        #region Fibonacci Stuff
        private void ChkFibonacciLoss_CheckedChanged(object sender, RoutedEventArgs e)
        {
            try
            {
                _ChkFibonacciLossIncrease = (bool)ChkFibonacciLossIncrease.IsChecked;
                _ChkFibonacciLossRestart = (bool)ChkFibonacciLossRestart.IsChecked;
                _ChkFibonacciLossStop = (bool)ChkFibonacciLossStop.IsChecked;
            }
            catch
            {
                // ignored
            }
        }
        private void ChkFibonacciWon_CheckedChanged(object sender, RoutedEventArgs e)
        {
            try
            {
                _ChkFibonacciWonIncrease = (bool)ChkFibonacciWonIncrease.IsChecked;
                _ChkFibonacciWonRestart = (bool)ChkFibonacciWonRestart.IsChecked;
                _ChkFibonacciWonStop = (bool)ChkFibonacciWonStop.IsChecked;
            }
            catch
            {
                // ignored
            }
        }
        private void NumFibonacciIncrementLoss_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double?> e)
        {
            try
            {
                _NumFibonacciIncrementLoss = (int)NumFibonacciIncrementLoss.Value;
            }
            catch
            {
                // ignored
            }
        }
        private void NumFibonacciIncrementWon_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double?> e)
        {
            try
            {
                _NumFibonacciIncrementWon = (int)NumFibonacciIncrementWon.Value;
            }
            catch
            {
                // ignored
            }
        }
        private void ChkFibonacciWhenLevel_CheckedChanged(object sender, RoutedEventArgs e)
        {
            try
            {
                _ChkFibonacciWhenLevel = (bool)ChkFibonacciWhenLevel.IsChecked;
            }
            catch
            {
                // ignored
            }
        }
        private void NumFibonacciWhenLevel_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double?> e)
        {
            try
            {
                _NumFibonacciWhenLevel = (int)NumFibonacciWhenLevel.Value;
            }
            catch
            {
                // ignored
            }
        }
        private void ChkFibonacciWhenLevelOption_CheckedChanged(object sender, RoutedEventArgs e)
        {
            try
            {
                _ChkFibonacciWhenLevelReset = (bool)ChkFibonacciWhenLevelReset.IsChecked;
                _ChkFibonacciWhenLevelStop = (bool)ChkFibonacciWhenLevelStop.IsChecked;
            }
            catch
            {
                // ignored
            }
        }

        #endregion

        #endregion

        #region Stop Conditions
        private void ChkStopConditions_CheckedChanged(object sender, RoutedEventArgs e)
        {
            try
            {
                _ChkBalanceLimit = (bool)ChkBalanceLimit.IsChecked;
                NumBalanceLimit.IsEnabled = _ChkBalanceLimit;
                _ChkBalanceLowerLimit = (bool)ChkBalanceLowerLimit.IsChecked;
                NumBalanceLowerLimit.IsEnabled = _ChkBalanceLowerLimit;
                _ChkStopAfterBTCLoss = (bool)ChkStopAfterBTCLoss.IsChecked;
                NumStopBTCLoss.IsEnabled = _ChkStopAfterBTCLoss;
                _ChkStopAfterLossesInRow = (bool)ChkStopAfterLossesInRow.IsChecked;
                NumStopLosesInRow.IsEnabled = _ChkStopAfterLossesInRow;
                _ChkResetAfterBTCLoss = (bool)ChkResetAfterBTCLoss.IsChecked;
                NumResetBTCLoss.IsEnabled = _ChkResetAfterBTCLoss;
                _ChkResetAfterLossesInRow = (bool)ChkResetAfterLossesInRow.IsChecked;
                NumResetLosesInRow.IsEnabled = _ChkResetAfterLossesInRow;
                _ChkStopAfterBTCProfit = (bool)ChkStopAfterBTCProfit.IsChecked;
                NumStopBTCProfit.IsEnabled = _ChkStopAfterBTCProfit;
                _ChkStopAfterWonsInRow = (bool)ChkStopAfterWonsInRow.IsChecked;
                NumStopWonsInRow.IsEnabled = _ChkStopAfterWonsInRow;
                _ChkResetAfterBTCProfit = (bool)ChkResetAfterBTCProfit.IsChecked;
                NumResetBTCProfit.IsEnabled = _ChkResetAfterBTCProfit;
                _ChkResetAfterWonsInRow = (bool)ChkResetAfterWonsInRow.IsChecked;
                NumResetWonsInRow.IsEnabled = _ChkResetAfterWonsInRow;
            }
            catch
            {
                // ignore
            }
        }
        private void NumStopConditions_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double?> e)
        {
            try
            {
                _NumBalanceLimit = (double)NumBalanceLimit.Value;
                _NumBalanceLowerLimit = (double)NumBalanceLowerLimit.Value;
                _NumStopBTCLoss = (double)NumStopBTCLoss.Value;
                _NumResetBTCLoss = (double)NumResetBTCLoss.Value;
                _NumStopBTCProfit = (double)NumStopBTCProfit.Value;
                _NumResetBTCProfit = (double)NumResetBTCProfit.Value;
                _NumStopLosesInRow = (double)NumStopLosesInRow.Value;
                _NumResetLosesInRow = (double)NumResetLosesInRow.Value;
                _NumStopWonsInRow = (double)NumStopWonsInRow.Value;
                _NumResetWonsInRow = (double)NumResetWonsInRow.Value;
            }
            catch
            {
                // ignored
            }
        }

        #endregion

        #region Others
        private void ChkAutoScroll_CheckedChanged(object sender, RoutedEventArgs e)
        {
            try
            {
                _AutoScroll = (bool)ChkAutoScroll.IsChecked;
            }
            catch
            {
                // ignored
            }
        }
        private void ChkShowWonsLosses_CheckedChanged(object sender, RoutedEventArgs e)
        {
            try
            {
                _ShowWons = (bool)ChkShowWons.IsChecked;
                _ShowLosses = (bool)ChkShowLosses.IsChecked;
            }
            catch
            {
                // ignored
            }
        }

        #endregion

        #region Several shit

        #region Method to format the data sent to Da Dice
        private static string ToServerString(double value, bool mode)
        {
            return mode ? value.ToString("0.00000000").Replace(",", ".") : value.ToString("0.00").Replace(",", ".");
        }
        #endregion
        #region Method to request a bet
        private GenericRoll RequestBet()
        {
            return _currentSite.Roll(_CurrentBet, _CurrentChance, _RollOverUnder.Equals("over"));
        }

        #endregion

        #endregion
        #region Method to update labels
        private void UpdateLabels()
        {
            VerifyTip();
            LblBalance.Content = _balance.ToString("0.00000000 BTC").Replace(",", ".");
            LblBiggestWon.Content = _BiggestWon.ToString("0.00000000 BTC").Replace(",", ".");
            LblBiggestLost.Content = _BiggestLost.ToString("0.00000000 BTC").Replace(",", ".");
            LblBiggestBet.Content = _BiggestBet.ToString("0.00000000 BTC").Replace(",", ".");
            LblWonCounter.Content = _WonsCounter;
            LblLostCounter.Content = _LostCounter;
            LblWinningStreakCounter.Content = _MaxWinStreak;
            LblLosingStreakCounter.Content = _MaxLossStreak;
            NumStartingBet.Maximum = _balance;
            NumAfterXLossesInRowChangeBetNumber.Maximum = _balance;
            NumAfterXWonsInRowChangeBetNumber.Maximum = _balance;
        }

        #endregion
        #region Method to show a normal async dialog
        /// <summary>
        /// This will show a normal async dialog with MahApps library
        /// </summary>
        /// <param name="title">The title of the dialog</param>
        /// <param name="message">The message of the dialog</param>
        private async void ShowNormalDialog(string title, string message)
        {
            await this.ShowMessageAsync(title, message);
        }

        #endregion
        #region Method to check if the user can send a tip
        private void VerifyTip()
        {
            if (_balance >= _currentSite.MinTipAmount)
            {
                lblTipMessage.Visibility = Visibility.Hidden;
                numAmountTip.Visibility = Visibility.Visible;
                numAmountTip.Maximum = (Math.Truncate((_balance * 100000000) / (_currentSite.MinTipAmount * 100000000)) * _currentSite.MinTipAmount);
                lblBtc.Visibility = Visibility.Visible;
                lblTipAmount.Visibility = Visibility.Visible;
                lblTipPayee.Visibility = Visibility.Visible;
                txtPayee.Visibility = Visibility.Visible;
                lblOrDonateMe.Visibility = Visibility.Visible;
                btnDonateMe.Visibility = Visibility.Visible;
                btnSendTip.Visibility = Visibility.Visible;
            }
            else
            {
                lblTipMessage.Visibility = Visibility.Visible;
                numAmountTip.Visibility = Visibility.Hidden;
                lblTipAmount.Visibility = Visibility.Hidden;
                lblBtc.Visibility = Visibility.Hidden;
                lblTipPayee.Visibility = Visibility.Hidden;
                txtPayee.Visibility = Visibility.Hidden;
                lblOrDonateMe.Visibility = Visibility.Hidden;
                btnDonateMe.Visibility = Visibility.Hidden;
                btnSendTip.Visibility = Visibility.Hidden;
            }

        }
        #endregion
        #region Method to calculate Payout giving Chance
        private static double CalculatePayout(double chance)
        {
            chance = Math.Round(chance, 2);

            var calculated = (100 / chance);
            calculated = calculated - (calculated / 100);
            calculated = Math.Round(calculated, 4);

            return calculated;
        }

        #endregion
        #region Methods related to Tip functionallities

        #region Event of clicking the Tip button
        private void btnTip_Click(object sender, RoutedEventArgs e)
        {
            FlyoutTip.IsOpen = !FlyoutTip.IsOpen;
        }

        #endregion
        #region Event of modifying the Payee
        private void txtPayee_TextChanged(object sender, TextChangedEventArgs e)
        {
            btnSendTip.IsEnabled = !txtPayee.Text.Equals(string.Empty);
        }

        #endregion
        #region Event of clicking "Donate me" button
        private void btnDonateMe_Click(object sender, RoutedEventArgs e)
        {
            txtPayee.Text = "sbarrenechea";
        }

        #endregion
        #region Event of clicking "Send tip" button
        private void btnSendTip_Click(object sender, RoutedEventArgs e)
        {
            FlyoutTip.IsOpen = false;
            _Payee = txtPayee.Text;
            _AmountTip = (double)numAmountTip.Value;
            FlyoutProcessExecution.IsOpen = true;
            ProgressTip.IsIndeterminate = true;
            ProgressTip.Visibility = Visibility.Visible;
            lblStatusTip.Content = string.Format("Sending tip to {0}...", _Payee);
            _tipWorker.RunWorkerAsync();
        }

        #endregion

        #endregion
        #region Event of clicking other buttons at topmost bar
        private void btnAbout_Click(object sender, RoutedEventArgs e)
        {
            flyoutAbout.IsOpen = true;
        }
        private void btnSettings_Click(object sender, RoutedEventArgs e)
        {
            flyoutSettings.IsOpen = true;
        }

        #region Methods related to change App style

        private void InitThemeSettings()
        {
            cmbTheme.ItemsSource = Enum.GetValues(typeof(Theme));
            cmbTheme.SelectedIndex = int.Parse(_parser.GetSetting("AUTODICE", "WINDOWTHEME"));
            cmbAccent.ItemsSource = Enum.GetValues(typeof(Accent));
            cmbAccent.SelectedIndex = int.Parse(_parser.GetSetting("AUTODICE", "WINDOWCOLOR"));
        }
        private void cmbTheme_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _parser.AddSetting("AUTODICE", "WINDOWTHEME", cmbTheme.SelectedIndex.ToString());
            _parser.SaveSettings();
            ChangeAppStyle();
        }

        private void cmbAccent_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _parser.AddSetting("AUTODICE", "WINDOWCOLOR", cmbAccent.SelectedIndex.ToString());
            _parser.SaveSettings();
            ChangeAppStyle();
        }
        private void ChangeAppStyle()
        {
            var a = int.Parse(_parser.GetSetting("AUTODICE", "WINDOWTHEME"));
            var b = int.Parse(_parser.GetSetting("AUTODICE", "WINDOWCOLOR"));
            string theme, accent;
            if (a == 0)
            {
                theme = "BaseLight";
                logoBlack.Visibility = Visibility.Hidden;
                logoWhite.Visibility = Visibility.Visible;
            }
            else
            {
                theme = "BaseDark";
                logoWhite.Visibility = Visibility.Hidden;
                logoBlack.Visibility = Visibility.Visible;
            }
            switch (b)
            {
                case 0:
                    accent = "Blue";
                    break;
                case 1:
                    accent = "Red";
                    break;
                case 2:
                    accent = "Green";
                    break;
                case 3:
                    accent = "Purple";
                    break;
                case 4:
                    accent = "Orange";
                    break;
                case 5:
                    accent = "Lime";
                    break;
                case 6:
                    accent = "Emerald";
                    break;
                case 7:
                    accent = "Teal";
                    break;
                case 8:
                    accent = "Cyan";
                    break;
                case 9:
                    accent = "Cobalt";
                    break;
                case 10:
                    accent = "Indigo";
                    break;
                case 11:
                    accent = "Violet";
                    break;
                case 12:
                    accent = "Pink";
                    break;
                case 13:
                    accent = "Magenta";
                    break;
                case 14:
                    accent = "Crimson";
                    break;
                case 15:
                    accent = "Amber";
                    break;
                case 16:
                    accent = "Yellow";
                    break;
                case 17:
                    accent = "Brown";
                    break;
                case 18:
                    accent = "Olive";
                    break;
                case 19:
                    accent = "Steel";
                    break;
                case 20:
                    accent = "Mauve";
                    break;
                case 21:
                    accent = "Taupe";
                    break;
                case 22:
                    accent = "Sienna";
                    break;
                default:
                    accent = "Blue";
                    break;
            }
            ThemeManager.ChangeAppStyle(Application.Current,
                                        ThemeManager.GetAccent(accent),
                                        ThemeManager.GetAppTheme(theme));
        }

        #endregion

        #endregion
        #region Methods to clear stuff
        private void BtnClearStatistics_Click(object sender, RoutedEventArgs e)
        {
            _MaxWinStreak = 0;
            _MaxLossStreak = 0;
            _NumMaxMultipliesLoss = 0;
            _NumMaxMultipliesWon = 0;
            _BiggestBet = 0;
            _BiggestLost = 0;
            _BiggestWon = 0;
            _WonsCounter = 0;
            _LostCounter = 0;
            UpdateLabels();
        }
        private void BtnClearGrid_Click(object sender, RoutedEventArgs e)
        {
            _datosRoll.Clear();
            GridBets.ItemsSource = null;
            GridBets.ItemsSource = _datosRoll;
        }

        #endregion

        #region Back to Normal Mode button
        private void btnNormalMode_Click(object sender, RoutedEventArgs e)
        {
            new ModeNormal(_currentSite, _username, _balance) { Visibility = Visibility.Visible };
            Close();
        }
        #endregion

        #endregion

        #region Load and Save methods
        private void SaveStratConfig(string filename)
        {

            using (var writer = new StreamWriter(filename))
            {
                #region Initial Settings
                writer.WriteLine("[INITIAL_SETTINGS]");
                writer.WriteLine("_NumStartingBet = {0}", _NumStartingBet);
                writer.WriteLine("_NumStartingChange = {0}", _NumStartingChange);
                writer.WriteLine("_RollOverUnder = {0}", _RollOverUnder);
                writer.WriteLine("_martingale = {0}", _martingale);
                writer.WriteLine("_fibonacci = {0}", _fibonacci);
                #endregion
                #region Stop Conditions
                writer.WriteLine("[STOP_CONDITIONS]");
                writer.WriteLine("_ChkBalanceLimit = {0}", _ChkBalanceLimit);
                writer.WriteLine("_NumBalanceLimit = {0}", _NumBalanceLimit);
                writer.WriteLine("_ChkBalanceLowerLimit = {0}", _ChkBalanceLowerLimit);
                writer.WriteLine("_NumBalanceLowerLimit = {0}", _NumBalanceLowerLimit);
                // Losses conditions
                writer.WriteLine("_ChkStopAfterBTCLoss = {0}", _ChkStopAfterBTCLoss);
                writer.WriteLine("_NumStopBTCLoss = {0}", _NumStopBTCLoss);
                writer.WriteLine("_ChkStopAfterLossesInRow = {0}", _ChkStopAfterLossesInRow);
                writer.WriteLine("_NumStopLosesInRow = {0}", _NumStopLosesInRow);
                writer.WriteLine("_ChkResetAfterBTCLoss = {0}", _ChkResetAfterBTCLoss);
                writer.WriteLine("_NumResetBTCLoss = {0}", _NumResetBTCLoss);
                writer.WriteLine("_ChkResetAfterLossesInRow = {0}", _ChkResetAfterLossesInRow);
                writer.WriteLine("_NumResetLosesInRow = {0}", _NumResetLosesInRow);
                // Wons conditions
                writer.WriteLine("_ChkStopAfterBTCProfit = {0}", _ChkStopAfterBTCProfit);
                writer.WriteLine("_NumStopBTCProfit = {0}", _NumStopBTCProfit);
                writer.WriteLine("_ChkStopAfterWonsInRow = {0}", _ChkStopAfterWonsInRow);
                writer.WriteLine("_NumStopWonsInRow = {0}", _NumStopWonsInRow);
                writer.WriteLine("_ChkResetAfterBTCProfit = {0}", _ChkResetAfterBTCProfit);
                writer.WriteLine("_NumResetBTCProfit = {0}", _NumResetBTCProfit);
                writer.WriteLine("_ChkResetAfterWonsInRow = {0}", _ChkResetAfterWonsInRow);
                writer.WriteLine("_NumResetWonsInRow = {0}", _NumResetWonsInRow);
                #endregion
                #region Martingale
                writer.WriteLine("[MARTINGALE]");
                #region Multiply on Loss
                writer.WriteLine("_NumMultiplierLoss = {0}", _NumMultiplierLoss);
                writer.WriteLine("_NumMaxMultipliesLoss = {0}", _NumMaxMultipliesLoss);
                writer.WriteLine("_NumAfterLoss = {0}", _NumAfterLoss);
                writer.WriteLine("_NumMultiplierAfterLoss = {0}", _NumMultiplierAfterLoss);
                writer.WriteLine("_RadMaxLoss = {0}", _RadMaxLoss);
                writer.WriteLine("_RadVariableLoss = {0}", _RadVariableLoss);
                writer.WriteLine("_RadConstrantLoss = {0}", _RadConstrantLoss);
                writer.WriteLine("_RadChangeOnceLoss = {0}", _RadChangeOnceLoss);
                writer.WriteLine("_ChkMultiplyOnlyOneTimeLoss = {0}", _ChkMultiplyOnlyOneTimeLoss);
                writer.WriteLine("_ChkAfterLossesInRowChangeBet = {0}", _ChkAfterLossesInRowChangeBet);
                writer.WriteLine("_NumAfterXLossesInRowChangeBet = {0}", _NumAfterXLossesInRowChangeBet);
                writer.WriteLine("_NumAfterXLossesInRowChangeBetNumber = {0}", _NumAfterXLossesInRowChangeBetNumber);
                writer.WriteLine("_ChkAfterLossesInRowChangeChance = {0}", _ChkAfterLossesInRowChangeChance);
                writer.WriteLine("_NumAfterXLossesInRowChangeChance = {0}", _NumAfterXLossesInRowChangeChance);
                writer.WriteLine("_NumAfterXLossesInRowChangeChanceNumber = {0}", _NumAfterXLossesInRowChangeChanceNumber);
                writer.WriteLine("_ChkReturnBaseBetAfterFirstLoss = {0}", _ChkReturnBaseBetAfterFirstLoss);
                #endregion
                #region Multiply on Won
                writer.WriteLine("_NumMultiplierWon = {0}", _NumMultiplierWon);
                writer.WriteLine("_NumMaxMultipliesWon = {0}", _NumMaxMultipliesWon);
                writer.WriteLine("_NumAfterWon = {0}", _NumAfterWon);
                writer.WriteLine("_NumMultiplierAfterWon = {0}", _NumMultiplierAfterWon);
                writer.WriteLine("_RadMaxWon = {0}", _RadMaxWon);
                writer.WriteLine("_RadVariableWon = {0}", _RadVariableWon);
                writer.WriteLine("_RadConstrantWon = {0}", _RadConstrantWon);
                writer.WriteLine("_RadChangeOnceWon = {0}", _RadChangeOnceWon);
                //writer.WriteLine("_ChkMultiplyOnlyOneTimeWon = {0}", _ChkMultiplyOnlyOneTimeWon);
                writer.WriteLine("_ChkAfterWonsInRowChangeBet = {0}", _ChkAfterWonsInRowChangeBet);
                writer.WriteLine("_NumAfterXWonsInRowChangeBet = {0}", _NumAfterXWonsInRowChangeBet);
                writer.WriteLine("_NumAfterXWonsInRowChangeBetNumber = {0}", _NumAfterXWonsInRowChangeBetNumber);
                writer.WriteLine("_ChkAfterWonsInRowChangeChance = {0}", _ChkAfterWonsInRowChangeChance);
                writer.WriteLine("_NumAfterXWonsInRowChangeChance = {0}", _NumAfterXWonsInRowChangeChance);
                writer.WriteLine("_NumAfterXWonsInRowChangeChanceNumber = {0}", _NumAfterXWonsInRowChangeChanceNumber);
                writer.WriteLine("_ChkReturnBaseBetAfterFirstWon = {0}", _ChkReturnBaseBetAfterFirstWon);
                #endregion
                #endregion
                #region Fibonacci
                writer.WriteLine("[FIBONACCI]");
                writer.WriteLine("_ChkFibonacciLossIncrease = {0}", _ChkFibonacciLossIncrease);
                writer.WriteLine("_NumFibonacciIncrementLoss = {0}", _NumFibonacciIncrementLoss);
                writer.WriteLine("_ChkFibonacciLossRestart = {0}", _ChkFibonacciLossRestart);
                writer.WriteLine("_ChkFibonacciLossStop = {0}", _ChkFibonacciLossStop);
                writer.WriteLine("_ChkFibonacciWonIncrease = {0}", _ChkFibonacciWonIncrease);
                writer.WriteLine("_NumFibonacciIncrementWon = {0}", _NumFibonacciIncrementWon);
                writer.WriteLine("_ChkFibonacciWonRestart = {0}", _ChkFibonacciWonRestart);
                writer.WriteLine("_ChkFibonacciWonStop = {0}", _ChkFibonacciWonStop);
                writer.WriteLine("_ChkFibonacciWhenLevel = {0}", _ChkFibonacciWhenLevel);
                writer.WriteLine("_NumFibonacciWhenLevel = {0}", _NumFibonacciWhenLevel);
                writer.WriteLine("_ChkFibonacciWhenLevelReset = {0}", _ChkFibonacciWhenLevelReset);
                writer.WriteLine("_ChkFibonacciWhenLevelStop = {0}", _ChkFibonacciWhenLevelStop);
                #endregion
                writer.Close();
            }
        }
        private void LoadStratConfig(string filename)
        {
            try
            {
                var parser = new IniParser(filename);
                #region Initial Settings
                NumStartingBet.Value = double.Parse(parser.GetSetting("INITIAL_SETTINGS", "_NumStartingBet").Replace(",", "."), CultureInfo.InvariantCulture);
                NumStartingChance.Value = double.Parse(parser.GetSetting("INITIAL_SETTINGS", "_NumStartingChange").Replace(",", "."), CultureInfo.InvariantCulture);
                _RollOverUnder = parser.GetSetting("INITIAL_SETTINGS", "_RollOverUnder");
                if (_RollOverUnder.ToLower().Equals("over"))
                {
                    RadRollOver.IsChecked = true;
                }
                else
                {
                    RadRollUnder.IsChecked = true;
                }
                _martingale = bool.Parse(parser.GetSetting("INITIAL_SETTINGS", "_martingale"));
                _fibonacci = bool.Parse(parser.GetSetting("INITIAL_SETTINGS", "_fibonacci"));
                if (_martingale)
                {
                    TabMartingale.IsSelected = true;
                }
                else if (_fibonacci)
                {
                    TabFibonacci.IsSelected = true;
                }
                #endregion
                #region Stop Conditions
                ChkBalanceLimit.IsChecked = bool.Parse(parser.GetSetting("STOP_CONDITIONS", "_ChkBalanceLimit"));
                NumBalanceLimit.Value = double.Parse(parser.GetSetting("STOP_CONDITIONS", "_NumBalanceLimit").Replace(",", "."), CultureInfo.InvariantCulture);
                ChkBalanceLowerLimit.IsChecked = bool.Parse(parser.GetSetting("STOP_CONDITIONS", "_ChkBalanceLowerLimit"));
                NumBalanceLowerLimit.Value = double.Parse(parser.GetSetting("STOP_CONDITIONS", "_NumBalanceLowerLimit").Replace(",", "."), CultureInfo.InvariantCulture);
                // Losses conditions
                ChkStopAfterBTCLoss.IsChecked = bool.Parse(parser.GetSetting("STOP_CONDITIONS", "_ChkStopAfterBTCLoss"));
                NumStopBTCLoss.Value = double.Parse(parser.GetSetting("STOP_CONDITIONS", "_NumStopBTCLoss").Replace(",", "."), CultureInfo.InvariantCulture);
                ChkStopAfterLossesInRow.IsChecked = bool.Parse(parser.GetSetting("STOP_CONDITIONS", "_ChkStopAfterLossesInRow"));
                NumStopLosesInRow.Value = double.Parse(parser.GetSetting("STOP_CONDITIONS", "_NumStopLosesInRow").Replace(",", "."), CultureInfo.InvariantCulture);
                ChkResetAfterBTCLoss.IsChecked = bool.Parse(parser.GetSetting("STOP_CONDITIONS", "_ChkResetAfterBTCLoss"));
                NumResetBTCLoss.Value = double.Parse(parser.GetSetting("STOP_CONDITIONS", "_NumResetBTCLoss").Replace(",", "."), CultureInfo.InvariantCulture);
                ChkResetAfterLossesInRow.IsChecked = bool.Parse(parser.GetSetting("STOP_CONDITIONS", "_ChkResetAfterLossesInRow"));
                NumResetLosesInRow.Value = double.Parse(parser.GetSetting("STOP_CONDITIONS", "_NumResetLosesInRow").Replace(",", "."), CultureInfo.InvariantCulture);
                // Wons conditions
                ChkStopAfterBTCProfit.IsChecked = bool.Parse(parser.GetSetting("STOP_CONDITIONS", "_ChkStopAfterBTCProfit"));
                NumStopBTCProfit.Value = double.Parse(parser.GetSetting("STOP_CONDITIONS", "_NumStopBTCProfit").Replace(",", "."), CultureInfo.InvariantCulture);
                ChkStopAfterWonsInRow.IsChecked = bool.Parse(parser.GetSetting("STOP_CONDITIONS", "_ChkStopAfterWonsInRow"));
                NumStopWonsInRow.Value = double.Parse(parser.GetSetting("STOP_CONDITIONS", "_NumStopWonsInRow").Replace(",", "."), CultureInfo.InvariantCulture);
                ChkResetAfterBTCProfit.IsChecked = bool.Parse(parser.GetSetting("STOP_CONDITIONS", "_ChkResetAfterBTCProfit"));
                NumResetBTCProfit.Value = double.Parse(parser.GetSetting("STOP_CONDITIONS", "_NumResetBTCProfit").Replace(",", "."), CultureInfo.InvariantCulture);
                ChkResetAfterWonsInRow.IsChecked = bool.Parse(parser.GetSetting("STOP_CONDITIONS", "_ChkResetAfterWonsInRow"));
                NumResetWonsInRow.Value = double.Parse(parser.GetSetting("STOP_CONDITIONS", "_NumResetWonsInRow").Replace(",", "."), CultureInfo.InvariantCulture);
                #endregion
                #region Martingale
                #region Multiply on Loss
                NumMultiplierLoss.Value = double.Parse(parser.GetSetting("MARTINGALE", "_NumMultiplierLoss").Replace(",", "."), CultureInfo.InvariantCulture);
                NumMaxMultipliesLoss.Value = double.Parse(parser.GetSetting("MARTINGALE", "_NumMaxMultipliesLoss").Replace(",", "."), CultureInfo.InvariantCulture);
                NumAfterLoss.Value = double.Parse(parser.GetSetting("MARTINGALE", "_NumAfterLoss").Replace(",", "."), CultureInfo.InvariantCulture);
                NumMultiplierAfterLoss.Value = double.Parse(parser.GetSetting("MARTINGALE", "_NumMultiplierAfterLoss").Replace(",", "."), CultureInfo.InvariantCulture);
                RadMaxLoss.IsChecked = bool.Parse(parser.GetSetting("MARTINGALE", "_RadMaxLoss"));
                RadVariableLoss.IsChecked = bool.Parse(parser.GetSetting("MARTINGALE", "_RadVariableLoss"));
                RadConstrantLoss.IsChecked = bool.Parse(parser.GetSetting("MARTINGALE", "_RadConstrantLoss"));
                RadChangeOnceLoss.IsChecked = bool.Parse(parser.GetSetting("MARTINGALE", "_RadChangeOnceLoss"));
                ChkMultiplyOnlyOneTimeLoss.IsChecked = bool.Parse(parser.GetSetting("MARTINGALE", "_ChkMultiplyOnlyOneTimeLoss"));
                ChkAfterLossesInRowChangeBet.IsChecked = bool.Parse(parser.GetSetting("MARTINGALE", "_ChkAfterLossesInRowChangeBet"));
                NumAfterXLossesInRowChangeBet.Value = double.Parse(parser.GetSetting("MARTINGALE", "_NumAfterXLossesInRowChangeBet").Replace(",", "."), CultureInfo.InvariantCulture);
                NumAfterXLossesInRowChangeBetNumber.Value = double.Parse(parser.GetSetting("MARTINGALE", "_NumAfterXLossesInRowChangeBetNumber").Replace(",", "."), CultureInfo.InvariantCulture);
                ChkAfterLossesInRowChangeChance.IsChecked = bool.Parse(parser.GetSetting("MARTINGALE", "_ChkAfterLossesInRowChangeChance"));
                NumAfterXLossesInRowChangeChance.Value = double.Parse(parser.GetSetting("MARTINGALE", "_NumAfterXLossesInRowChangeChance").Replace(",", "."), CultureInfo.InvariantCulture);
                NumAfterXLossesInRowChangeChanceNumber.Value = double.Parse(parser.GetSetting("MARTINGALE", "_NumAfterXLossesInRowChangeChanceNumber").Replace(",", "."), CultureInfo.InvariantCulture);
                ChkReturnBaseBetAfterFirstLoss.IsChecked = bool.Parse(parser.GetSetting("MARTINGALE", "_ChkReturnBaseBetAfterFirstLoss"));
                #endregion
                #region Multiply on Won
                NumMultiplierWon.Value = double.Parse(parser.GetSetting("MARTINGALE", "_NumMultiplierWon").Replace(",", "."), CultureInfo.InvariantCulture);
                NumMaxMultipliesWon.Value = double.Parse(parser.GetSetting("MARTINGALE", "_NumMaxMultipliesWon").Replace(",", "."), CultureInfo.InvariantCulture);
                NumAfterWon.Value = double.Parse(parser.GetSetting("MARTINGALE", "_NumAfterWon").Replace(",", "."), CultureInfo.InvariantCulture);
                NumMultiplierAfterWon.Value = double.Parse(parser.GetSetting("MARTINGALE", "_NumMultiplierAfterWon").Replace(",", "."), CultureInfo.InvariantCulture);
                RadMaxWon.IsChecked = bool.Parse(parser.GetSetting("MARTINGALE", "_RadMaxWon"));
                RadVariableWon.IsChecked = bool.Parse(parser.GetSetting("MARTINGALE", "_RadVariableWon"));
                RadConstrantWon.IsChecked = bool.Parse(parser.GetSetting("MARTINGALE", "_RadConstrantWon"));
                RadChangeOnceWon.IsChecked = bool.Parse(parser.GetSetting("MARTINGALE", "_RadChangeOnceWon"));
                ChkAfterWonsInRowChangeBet.IsChecked = bool.Parse(parser.GetSetting("MARTINGALE", "_ChkAfterWonsInRowChangeBet"));
                NumAfterXWonsInRowChangeBet.Value = double.Parse(parser.GetSetting("MARTINGALE", "_NumAfterXWonsInRowChangeBet").Replace(",", "."), CultureInfo.InvariantCulture);
                NumAfterXWonsInRowChangeBetNumber.Value = double.Parse(parser.GetSetting("MARTINGALE", "_NumAfterXWonsInRowChangeBetNumber").Replace(",", "."), CultureInfo.InvariantCulture);
                ChkAfterWonsInRowChangeChance.IsChecked = bool.Parse(parser.GetSetting("MARTINGALE", "_ChkAfterWonsInRowChangeChance"));
                NumAfterXWonsInRowChangeChance.Value = double.Parse(parser.GetSetting("MARTINGALE", "_NumAfterXWonsInRowChangeChance").Replace(",", "."), CultureInfo.InvariantCulture);
                NumAfterXWonsInRowChangeChanceNumber.Value = double.Parse(parser.GetSetting("MARTINGALE", "_NumAfterXWonsInRowChangeChanceNumber").Replace(",", "."), CultureInfo.InvariantCulture);
                ChkReturnBaseBetAfterFirstWon.IsChecked = bool.Parse(parser.GetSetting("MARTINGALE", "_ChkReturnBaseBetAfterFirstWon"));
                #endregion
                #endregion
                #region Fibonacci
                ChkFibonacciLossIncrease.IsChecked = bool.Parse(parser.GetSetting("FIBONACCI", "_ChkFibonacciLossIncrease"));
                NumFibonacciIncrementLoss.Value = double.Parse(parser.GetSetting("FIBONACCI", "_NumFibonacciIncrementLoss").Replace(",", "."), CultureInfo.InvariantCulture);
                ChkFibonacciLossRestart.IsChecked = bool.Parse(parser.GetSetting("FIBONACCI", "_ChkFibonacciLossRestart"));
                ChkFibonacciLossStop.IsChecked = bool.Parse(parser.GetSetting("FIBONACCI", "_ChkFibonacciLossStop"));
                ChkFibonacciWonIncrease.IsChecked = bool.Parse(parser.GetSetting("FIBONACCI", "_ChkFibonacciWonIncrease"));
                NumFibonacciIncrementWon.Value = double.Parse(parser.GetSetting("FIBONACCI", "_NumFibonacciIncrementWon").Replace(",", "."), CultureInfo.InvariantCulture);
                ChkFibonacciWonRestart.IsChecked = bool.Parse(parser.GetSetting("FIBONACCI", "_ChkFibonacciWonRestart"));
                ChkFibonacciWonStop.IsChecked = bool.Parse(parser.GetSetting("FIBONACCI", "_ChkFibonacciWonStop"));
                ChkFibonacciWhenLevel.IsChecked = bool.Parse(parser.GetSetting("FIBONACCI", "_ChkFibonacciWhenLevel"));
                NumFibonacciWhenLevel.Value = double.Parse(parser.GetSetting("FIBONACCI", "_NumFibonacciWhenLevel").Replace(",", "."), CultureInfo.InvariantCulture);
                ChkFibonacciWhenLevelReset.IsChecked = bool.Parse(parser.GetSetting("FIBONACCI", "_ChkFibonacciWhenLevelReset"));
                ChkFibonacciWhenLevelStop.IsChecked = bool.Parse(parser.GetSetting("FIBONACCI", "_ChkFibonacciWhenLevelStop"));
                #endregion
            }
            catch
            {
                ShowNormalDialog("Error", "An error occured while parsing the Config File.");
            }
        }
        #region Buttons
        private void btnLoadSave_Click(object sender, RoutedEventArgs e)
        {
            FlyoutLoadSave.IsOpen = true;
        }
        private void BtnLoadStrategie_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                DefaultExt = ".ini",
                Filter = "Strategie Config File|*.ini",
                InitialDirectory = Environment.CurrentDirectory
            };
            var result = dlg.ShowDialog();

            if (result != true) return;
            LoadStratConfig(dlg.FileName);
            FlyoutLoadSave.IsOpen = false;
        }
        private void BtnSaveStrategie_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new SaveFileDialog
            {
                FileName = "Strategie",
                DefaultExt = ".ini",
                Filter = "Strategie Config File (.ini)|*.ini",
                InitialDirectory = Environment.CurrentDirectory
            };
            var result = dlg.ShowDialog();

            if (result != true) return;
            SaveStratConfig(dlg.FileName);
            FlyoutLoadSave.IsOpen = false;
        }
        #endregion


        #endregion

        #region Restart Graph
        private void RestartGraph()
        {
            lblAverageProfit.Content = string.Empty;
            Profit.Points.Clear();
        }
        #endregion
    }
}