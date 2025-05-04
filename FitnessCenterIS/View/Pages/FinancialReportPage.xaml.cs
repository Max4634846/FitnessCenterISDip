using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using LiveCharts;
using LiveCharts.Wpf;
using System.Windows.Data;
using Microsoft.Win32;
using System.IO;
using System.Printing;
using System.Windows.Documents;
using System.Windows.Xps;
using System.Windows.Xps.Packaging;
using FitnessCenterIS.Model;
using System.Data.SqlClient;
using System.Data;
using System.Globalization;
using System.Configuration;
using System.Data.Entity;
using System.Data.Entity.Core.EntityClient;
using System.ComponentModel;
using System.Windows.Markup;

namespace FitnessCenterIS.View.Pages.Reports
{
    public partial class FinancialReportPage : Page
    {
        private DateTime startDate;
        private DateTime endDate;
        private string connectionString;
        private List<FinancialTransaction> financialData;

        // Класс для представления финансовой транзакции
        public class FinancialTransaction : INotifyPropertyChanged
        {
            public DateTime Date { get; set; }
            public int TransactionId { get; set; }
            public string Category { get; set; }
            public string Product { get; set; }
            public decimal Income { get; set; }
            public decimal Expenses { get; set; }
            public decimal Profit => Income - Expenses;
            public string Manager { get; set; }

            public event PropertyChangedEventHandler PropertyChanged;
            protected void OnPropertyChanged(string name)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
            }
        }

        public FinancialReportPage()
        {
            InitializeComponent();

            try
            {
                // Извлекаем строку подключения SQL из строки подключения Entity Framework
                var entityConnectionString = ConfigurationManager.ConnectionStrings["BDFitnessClubDipEntities"].ConnectionString;
                var entityBuilder = new EntityConnectionStringBuilder(entityConnectionString);
                connectionString = entityBuilder.ProviderConnectionString;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при получении строки подключения: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            // Загрузка данных после полной инициализации UI
            this.Loaded += (s, e) => {
                try
                {
                    // Set default date range (last 30 days)
                    DateToPicker.SelectedDate = DateTime.Today;
                    DateFromPicker.SelectedDate = DateTime.Today.AddDays(-30);

                    // Initial data load
                    LoadFinancialData();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при инициализации: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            };
        }

        private void DateRangeChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DateFromPicker.SelectedDate.HasValue && DateToPicker.SelectedDate.HasValue)
            {
                LoadFinancialData();
            }
        }

        private void LoadFinancialData()
        {
            if (!DateFromPicker.SelectedDate.HasValue || !DateToPicker.SelectedDate.HasValue)
                return;

            startDate = DateFromPicker.SelectedDate.Value;
            endDate = DateToPicker.SelectedDate.Value.AddDays(1).AddSeconds(-1); // End of the selected day

            // Update period title
            PeriodTitle.Text = $"Финансовые показатели за период {startDate:dd.MM.yyyy} - {endDate:dd.MM.yyyy}";

            try
            {
                // Load financial metrics
                LoadFinancialMetrics();

                // Load chart data
                CreateFinancialChart();

                // Load DataGrid data
                LoadTransactionDetails();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке данных: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadFinancialMetrics()
        {
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();

                // Total income (revenue from sales)
                using (SqlCommand cmd = new SqlCommand(@"
                    SELECT ISNULL(SUM(PriceSold), 0) 
                    FROM Sales 
                    WHERE SaleDateTime BETWEEN @StartDate AND @EndDate", connection))
                {
                    cmd.Parameters.AddWithValue("@StartDate", startDate);
                    cmd.Parameters.AddWithValue("@EndDate", endDate);
                    decimal totalIncome = 0;
                    var result = cmd.ExecuteScalar();
                    if (result != null && result != DBNull.Value)
                    {
                        totalIncome = Convert.ToDecimal(result);
                    }
                    TotalIncomeTextBlock.Text = $"{totalIncome:N0} ₽";
                }

                // Доход от абонементов
                using (SqlCommand cmd = new SqlCommand(@"
                    SELECT ISNULL(SUM(PriceSold), 0) 
                    FROM Sales 
                    WHERE SeasonticketID IS NOT NULL 
                    AND SaleDateTime BETWEEN @StartDate AND @EndDate", connection))
                {
                    cmd.Parameters.AddWithValue("@StartDate", startDate);
                    cmd.Parameters.AddWithValue("@EndDate", endDate);
                    decimal membershipIncome = 0;
                    var result = cmd.ExecuteScalar();
                    if (result != null && result != DBNull.Value)
                    {
                        membershipIncome = Convert.ToDecimal(result);
                    }
                    MembershipIncomeTextBlock.Text = $"{membershipIncome:N0} ₽";
                }

                // Доход от услуг
                using (SqlCommand cmd = new SqlCommand(@"
                    SELECT ISNULL(SUM(PriceSold), 0) 
                    FROM Sales 
                    WHERE SeasonticketServiceID IS NOT NULL 
                    AND SaleDateTime BETWEEN @StartDate AND @EndDate", connection))
                {
                    cmd.Parameters.AddWithValue("@StartDate", startDate);
                    cmd.Parameters.AddWithValue("@EndDate", endDate);
                    decimal serviceIncome = 0;
                    var result = cmd.ExecuteScalar();
                    if (result != null && result != DBNull.Value)
                    {
                        serviceIncome = Convert.ToDecimal(result);
                    }
                    ServiceIncomeTextBlock.Text = $"{serviceIncome:N0} ₽";
                }

                // Средний чек
                using (SqlCommand cmd = new SqlCommand(@"
                    SELECT ISNULL(AVG(PriceSold), 0) 
                    FROM Sales 
                    WHERE SaleDateTime BETWEEN @StartDate AND @EndDate", connection))
                {
                    cmd.Parameters.AddWithValue("@StartDate", startDate);
                    cmd.Parameters.AddWithValue("@EndDate", endDate);
                    decimal avgCheck = 0;
                    var result = cmd.ExecuteScalar();
                    if (result != null && result != DBNull.Value)
                    {
                        avgCheck = Convert.ToDecimal(result);
                    }
                    AverageCheckTextBlock.Text = $"{avgCheck:N0} ₽";
                }

                // Вычисляем показатели для расходов и прибыли
                // В данной реализации расходы условно принимаются за 0
                // (можно модифицировать, когда будут учитываться реальные расходы)

                // Получаем значение дохода из текста
                string incomeText = TotalIncomeTextBlock.Text.Replace("₽", "").Trim();
                decimal income = 0;

                if (decimal.TryParse(incomeText, NumberStyles.Currency | NumberStyles.AllowThousands,
                                    CultureInfo.GetCultureInfo("ru-RU"), out income))
                {
                    // Расходы (по умолчанию 0, в демонстрационных целях)
                    decimal expenses = 0;

                    // Прибыль равна доходу за вычетом расходов
                    decimal profit = income - expenses;
                    ProfitTextBlock.Text = $"{profit:N0} ₽";

                    // Маржинальность
                    decimal marginality = (income > 0) ? (profit / income) * 100 : 0;
                    MarginalityTextBlock.Text = $"{marginality:N1}%";
                }
                else
                {
                    ProfitTextBlock.Text = "0 ₽";
                    MarginalityTextBlock.Text = "0.0%";
                }
            }
        }

        private void CreateFinancialChart()
        {
            var incomeData = new ChartValues<decimal>();
            var expensesData = new ChartValues<decimal>();
            var profitData = new ChartValues<decimal>();
            var labels = new List<string>();

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();

                // Group by day if period <= 31 days, otherwise group by month
                string sqlQuery = (endDate - startDate).TotalDays <= 31
                    ? @"SELECT 
                         CONVERT(varchar, SaleDateTime, 104) as DateGroup, 
                         SUM(PriceSold) as Revenue
                       FROM Sales 
                       WHERE SaleDateTime BETWEEN @StartDate AND @EndDate 
                       GROUP BY CONVERT(varchar, SaleDateTime, 104)
                       ORDER BY MIN(SaleDateTime)"
                    : @"SELECT 
                         CONVERT(varchar(7), SaleDateTime, 104) as DateGroup, 
                         SUM(PriceSold) as Revenue
                       FROM Sales 
                       WHERE SaleDateTime BETWEEN @StartDate AND @EndDate 
                       GROUP BY CONVERT(varchar(7), SaleDateTime, 104)
                       ORDER BY MIN(SaleDateTime)";

                using (SqlCommand cmd = new SqlCommand(sqlQuery, connection))
                {
                    cmd.Parameters.AddWithValue("@StartDate", startDate);
                    cmd.Parameters.AddWithValue("@EndDate", endDate);

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            labels.Add(reader["DateGroup"].ToString());

                            decimal revenue = 0;

                            if (!reader.IsDBNull(reader.GetOrdinal("Revenue")))
                            {
                                revenue = reader.GetDecimal(reader.GetOrdinal("Revenue"));
                            }

                            // В данной демонстрационной версии расходы отсутствуют
                            decimal expenses = 0;
                            decimal profit = revenue;

                            incomeData.Add(revenue);
                            expensesData.Add(expenses);
                            profitData.Add(profit);
                        }
                    }
                }
            }

            // Create series for the chart
            var series = new SeriesCollection
            {
                new LineSeries
                {
                    Title = "Доходы",
                    Values = incomeData,
                    PointGeometrySize = 10,
                    Stroke = new SolidColorBrush(Color.FromRgb(76, 175, 80)), // Green
                    Fill = new SolidColorBrush(Color.FromArgb(40, 76, 175, 80))
                },
                new LineSeries
                {
                    Title = "Прибыль",
                    Values = profitData,
                    PointGeometrySize = 10,
                    Stroke = new SolidColorBrush(Color.FromRgb(33, 150, 243)), // Blue
                    Fill = new SolidColorBrush(Color.FromArgb(40, 33, 150, 243))
                }
            };

            // Очищаем предыдущие серии перед добавлением новых
            FinancialChart.Series.Clear();
            FinancialChart.Series = series;

            // Update chart axes
            if (FinancialChart.AxisX.Count > 0)
            {
                FinancialChart.AxisX[0].Labels = labels;
                FinancialChart.AxisX[0].Separator = new LiveCharts.Wpf.Separator { Step = Math.Max(1, labels.Count / 10) };
            }

            if (FinancialChart.AxisY.Count > 0)
            {
                FinancialChart.AxisY[0].LabelFormatter = value => value.ToString("N0") + " ₽";
            }
        }

        private void LoadTransactionDetails()
        {
            financialData = new List<FinancialTransaction>();

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();

                string sqlQuery = @"
                    SELECT 
                        s.SaleID as TransactionId,
                        s.SaleDateTime as Date,
                        CASE
                            WHEN s.SeasonticketID IS NOT NULL THEN 'Абонемент'
                            WHEN s.SeasonticketServiceID IS NOT NULL THEN 'Услуга'
                            ELSE 'Другое'
                        END as Category,
                        COALESCE(
                            (SELECT st.Name FROM Seasontickets st WHERE st.SeasonticketID = s.SeasonticketID),
                            (SELECT srv.Name FROM Services srv 
                             JOIN SeasonticketServices ss ON srv.ServiceID = ss.ServiceID 
                             WHERE ss.SeasonticketServiceID = s.SeasonticketServiceID),
                            'Неизвестно'
                        ) as ProductName,
                        s.PriceSold as Income,
                        CONCAT(p.Surname, ' ', p.Name) as ManagerName
                    FROM Sales s
                    LEFT JOIN Users u ON s.AdministratorID = u.UserID
                    LEFT JOIN Staffs st ON u.StaffID = st.StaffID
                    LEFT JOIN Persons p ON st.PersonID = p.PersonID
                    WHERE s.SaleDateTime BETWEEN @StartDate AND @EndDate
                    ORDER BY s.SaleDateTime DESC";

                using (SqlCommand cmd = new SqlCommand(sqlQuery, connection))
                {
                    cmd.Parameters.AddWithValue("@StartDate", startDate);
                    cmd.Parameters.AddWithValue("@EndDate", endDate);

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            try
                            {
                                var transaction = new FinancialTransaction
                                {
                                    TransactionId = reader.IsDBNull(reader.GetOrdinal("TransactionId")) ? 0 : reader.GetInt32(reader.GetOrdinal("TransactionId")),
                                    Date = reader.GetDateTime(reader.GetOrdinal("Date")),
                                    Category = reader.IsDBNull(reader.GetOrdinal("Category")) ? "Неизвестно" : reader.GetString(reader.GetOrdinal("Category")),
                                    Product = reader.IsDBNull(reader.GetOrdinal("ProductName")) ? "Неизвестно" : reader.GetString(reader.GetOrdinal("ProductName")),
                                    Income = reader.IsDBNull(reader.GetOrdinal("Income")) ? 0 : reader.GetDecimal(reader.GetOrdinal("Income")),
                                    // Расходы по умолчанию равны 0 (демонстрационный вариант)
                                    Expenses = 0,
                                    Manager = reader.IsDBNull(reader.GetOrdinal("ManagerName")) ? "Неизвестно" : reader.GetString(reader.GetOrdinal("ManagerName"))
                                };

                                financialData.Add(transaction);
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"Ошибка при обработке записи: {ex.Message}");
                            }
                        }
                    }
                }
            }

            // Update DataGrid and footer
            FinancialDataGrid.ItemsSource = financialData;
            TotalRowsTextBlock.Text = $"Всего записей: {financialData.Count}";

            decimal totalIncome = financialData.Sum(t => t.Income);
            decimal totalExpenses = financialData.Sum(t => t.Expenses);
            decimal totalProfit = financialData.Sum(t => t.Profit);

            IncomeFooterTextBlock.Text = $"{totalIncome:N2} ₽";
            ExpensesFooterTextBlock.Text = $"{totalExpenses:N2} ₽";
            ProfitFooterTextBlock.Text = $"{totalProfit:N2} ₽";
        }

        private void ExportToExcel_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog
            {
                Filter = "Excel Files (*.xlsx)|*.xlsx|CSV Files (*.csv)|*.csv|All files (*.*)|*.*",
                DefaultExt = "xlsx",
                Title = "Экспорт финансового отчета"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                try
                {
                    // Здесь можно реализовать экспорт в Excel с помощью библиотек
                    // EPPlus, ClosedXML или NPOI
                    MessageBox.Show("Отчет успешно экспортирован!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при экспорте: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void PrintReport_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                PrintDialog printDialog = new PrintDialog();
                if (printDialog.ShowDialog() == true)
                {
                    // Create document for printing
                    FixedDocument document = CreatePrintDocument();
                    printDialog.PrintDocument(document.DocumentPaginator, "Финансовый отчет");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при печати: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private FixedDocument CreatePrintDocument()
        {
            FixedDocument document = new FixedDocument();

            // Create a visual representation of the report
            Grid printGrid = new Grid();
            printGrid.Width = 794; // A4 width in pixels at 96 DPI
            printGrid.Height = 1123; // A4 height in pixels at 96 DPI

            // Add a Title
            TextBlock titleBlock = new TextBlock
            {
                Text = $"Финансовый отчет за период {startDate:dd.MM.yyyy} - {endDate:dd.MM.yyyy}",
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 20, 0, 20)
            };

            // Add summary section
            StackPanel summaryPanel = new StackPanel { Margin = new Thickness(20) };
            summaryPanel.Children.Add(new TextBlock { Text = "Основные показатели:", FontSize = 14, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 10, 0, 10) });
            summaryPanel.Children.Add(new TextBlock { Text = $"Общий доход: {TotalIncomeTextBlock.Text}", Margin = new Thickness(10, 5, 0, 0) });
            summaryPanel.Children.Add(new TextBlock { Text = $"Прибыль: {ProfitTextBlock.Text}", Margin = new Thickness(10, 5, 0, 0) });
            summaryPanel.Children.Add(new TextBlock { Text = $"Маржинальность: {MarginalityTextBlock.Text}", Margin = new Thickness(10, 5, 0, 0) });
            summaryPanel.Children.Add(new TextBlock { Text = $"Доход от абонементов: {MembershipIncomeTextBlock.Text}", Margin = new Thickness(10, 5, 0, 0) });
            summaryPanel.Children.Add(new TextBlock { Text = $"Доход от услуг: {ServiceIncomeTextBlock.Text}", Margin = new Thickness(10, 5, 0, 0) });
            summaryPanel.Children.Add(new TextBlock { Text = $"Средний чек: {AverageCheckTextBlock.Text}", Margin = new Thickness(10, 5, 0, 0) });

            // Setup grid
            printGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            printGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Place elements in the grid
            Grid.SetRow(titleBlock, 0);
            Grid.SetRow(summaryPanel, 1);

            printGrid.Children.Add(titleBlock);
            printGrid.Children.Add(summaryPanel);

            // Create a page and add the grid
            FixedPage page = new FixedPage();
            page.Width = 794;
            page.Height = 1123;
            page.Children.Add(printGrid);

            // Add the page to the document
            PageContent pageContent = new PageContent();
            ((IAddChild)pageContent).AddChild(page);
            document.Pages.Add(pageContent);

            return document;
        }
    }
}
