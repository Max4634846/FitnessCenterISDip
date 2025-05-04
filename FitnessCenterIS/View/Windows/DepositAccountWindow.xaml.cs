using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using FitnessCenterIS.Model;

namespace FitnessCenterIS.View.Windows
{
    public partial class DepositAccountWindow : Window
    {
        private int _clientId;
        private Clients _currentClient;
        private decimal _availableBonus;
        private decimal _availableDeposit;
        private decimal _totalAmount;
        private string _operationType;

        public DepositAccountWindow(int clientId, decimal amount = 0, string operationType = "Deposit")
        {
            InitializeComponent();
            _clientId = clientId;
            _totalAmount = amount;
            _operationType = operationType;

            this.Loaded += (s, e) => LoadClientData();
        }

        private void LoadClientData()
        {
            using (var context = new BDFitnessClubDipEntities())
            {
                _currentClient = context.Clients.FirstOrDefault(c => c.ClientID == _clientId);
                if (_currentClient != null)
                {
                    // Set client information
                    ClientNameTextBlock.Text = $"{_currentClient.Persons.Surname} {_currentClient.Persons.Name} {_currentClient.Persons.MiddleName}";
                    _availableBonus = _currentClient.BonuseBalance ?? 0;
                    _availableDeposit = _currentClient.DepositBalance ?? 0;

                    // Update UI
                    AvailableBonusTextBlock.Text = $"{_availableBonus:N2}";
                    AvailableDepositTextBlock.Text = $"{_availableDeposit:N2} ₽";
                    TotalAmountTextBlock.Text = $"{_totalAmount:N2} ₽";

                    // Load payment methods for combobox
                    var paymentMethods = context.PaymentMethods.ToList();
                    PaymentMethodComboBox.ItemsSource = paymentMethods;
                    PaymentMethodComboBox.DisplayMemberPath = "Name";
                    PaymentMethodComboBox.SelectedValuePath = "PaymentMethodID";

                    if (paymentMethods.Count > 0)
                        PaymentMethodComboBox.SelectedIndex = 0;

                    // Configure UI based on operation type
                    if (_operationType == "Deposit")
                    {
                        Title = "Пополнение депозитного счета";
                        OperationTypeTextBlock.Text = "Пополнение счета";
                        SplitPaymentPanel.Visibility = Visibility.Collapsed;
                        BonusPaymentPanel.Visibility = Visibility.Collapsed;
                        AmountTextBox.Text = "";
                        AmountTextBox.IsEnabled = true;
                    }
                    else // Payment or Withdrawal
                    {
                        Title = "Оплата с депозитного счета";
                        OperationTypeTextBlock.Text = "Оплата услуг";
                        SplitPaymentPanel.Visibility = Visibility.Visible;
                        BonusPaymentPanel.Visibility = Visibility.Visible;
                        AmountTextBox.Text = _totalAmount.ToString();
                        AmountTextBox.IsEnabled = false;

                        // Set deposit max based on available balance - convert decimal to double for slider
                        DepositAmountSlider.Maximum = (double)Math.Min(_availableDeposit, _totalAmount);
                        BonusAmountSlider.Maximum = (double)Math.Min(_availableBonus, _totalAmount);

                        UpdatePaymentSummary();
                    }
                }
                else
                {
                    MessageBox.Show("Клиент не найден.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    Close();
                }
            }
        }

        private void AmountTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (decimal.TryParse(AmountTextBox.Text, out decimal amount))
            {
                _totalAmount = amount;
                TotalAmountTextBlock.Text = $"{_totalAmount:N2} ₽";

                if (_operationType != "Deposit")
                {
                    // Convert decimal to double for slider
                    DepositAmountSlider.Maximum = (double)Math.Min(_availableDeposit, _totalAmount);
                    BonusAmountSlider.Maximum = (double)Math.Min(_availableBonus, _totalAmount);
                    UpdatePaymentSummary();
                }
            }
        }

        private void DepositAmountSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            UpdatePaymentSummary();
        }

        private void BonusAmountSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            UpdatePaymentSummary();
        }

        private void UpdatePaymentSummary()
        {
            // Convert from double to decimal
            decimal depositAmount = (decimal)DepositAmountSlider.Value;
            decimal bonusAmount = (decimal)BonusAmountSlider.Value;
            decimal cardAmount = _totalAmount - depositAmount - bonusAmount;

            DepositAmountTextBlock.Text = $"{depositAmount:N2} ₽";
            BonusAmountTextBlock.Text = $"{bonusAmount:N2}";
            CardAmountTextBlock.Text = $"{cardAmount:N2} ₽";

            // Validate total amount
            if (cardAmount < 0)
            {
                ConfirmButton.IsEnabled = false;
                ErrorTextBlock.Text = "Общая сумма платежей превышает требуемую сумму";
                ErrorTextBlock.Visibility = Visibility.Visible;
            }
            else
            {
                ConfirmButton.IsEnabled = true;
                ErrorTextBlock.Visibility = Visibility.Collapsed;
            }
        }

        private void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            DepositAmountSlider.Value = 0;
            BonusAmountSlider.Value = 0;
            UpdatePaymentSummary();
        }

        private void ConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            if (PaymentMethodComboBox.SelectedValue == null)
            {
                MessageBox.Show("Выберите способ оплаты.", "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            using (var context = new BDFitnessClubDipEntities())
            {
                var client = context.Clients.FirstOrDefault(c => c.ClientID == _clientId);
                if (client == null)
                {
                    MessageBox.Show("Клиент не найден.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                int paymentMethodId = (int)PaymentMethodComboBox.SelectedValue;

                if (_operationType == "Deposit")
                {
                    // Deposit operation
                    if (!decimal.TryParse(AmountTextBox.Text, out decimal amount) || amount <= 0)
                    {
                        MessageBox.Show("Введите корректную сумму для пополнения.", "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    // Update client deposit balance
                    client.DepositBalance = (client.DepositBalance ?? 0) + amount;
                    context.SaveChanges();

                    MessageBox.Show($"Депозит успешно пополнен на сумму {amount:N2} ₽!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else // Payment or Withdrawal
                {
                    // Convert from double to decimal
                    decimal depositAmount = (decimal)DepositAmountSlider.Value;
                    decimal bonusAmount = (decimal)BonusAmountSlider.Value;
                    decimal cardAmount = _totalAmount - depositAmount - bonusAmount;

                    // Check if we have enough balance
                    if (client.DepositBalance < depositAmount)
                    {
                        MessageBox.Show("Недостаточно средств на депозитном счете.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    if (client.BonuseBalance < bonusAmount)
                    {
                        MessageBox.Show("Недостаточно бонусных баллов.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    // Update client balances
                    if (depositAmount > 0)
                        client.DepositBalance -= depositAmount;

                    if (bonusAmount > 0)
                        client.BonuseBalance -= bonusAmount;

                    context.SaveChanges();

                    // Set result properties for the calling window
                    PaymentResult = new PaymentResult
                    {
                        Success = true,
                        DepositAmount = depositAmount,
                        BonusAmount = bonusAmount,
                        CardAmount = cardAmount,
                        PaymentMethodId = paymentMethodId
                    };

                    MessageBox.Show("Оплата успешно выполнена!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                }

                DialogResult = true;
                Close();
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        public PaymentResult PaymentResult { get; private set; }
    }

    // Result class to return payment details to calling window
    public class PaymentResult
    {
        public bool Success { get; set; }
        public decimal DepositAmount { get; set; }
        public decimal BonusAmount { get; set; }
        public decimal CardAmount { get; set; }
        public int PaymentMethodId { get; set; }
    }
}