using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Windows;
using System.Windows.Controls;
using LiveCharts;
using LiveCharts.Wpf;
using System.Configuration;
using System.Data.Entity.Core.EntityClient;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Table = DocumentFormat.OpenXml.Wordprocessing.Table;
using TableRow = DocumentFormat.OpenXml.Wordprocessing.TableRow;
using TableCell = DocumentFormat.OpenXml.Wordprocessing.TableCell;
using BorderValues = DocumentFormat.OpenXml.Wordprocessing.BorderValues;
using Separator = LiveCharts.Wpf.Separator;

namespace FitnessCenterIS.View.Pages
{
    public partial class TrainerReportPage : Page
    {
        private DateTime startDate;
        private DateTime endDate;
        private string connectionString;

        public TrainerReportPage()
        {
            InitializeComponent();

            try
            {
                // Получаем строку подключения
                var entityConnectionString = ConfigurationManager.ConnectionStrings["BDFitnessClubDipEntities"].ConnectionString;
                var entityBuilder = new EntityConnectionStringBuilder(entityConnectionString);
                connectionString = entityBuilder.ProviderConnectionString;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка подключения: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            // Инициализация с датами по умолчанию
            this.Loaded += (s, e) => {
                DateToPicker.SelectedDate = DateTime.Today;
                DateFromPicker.SelectedDate = DateTime.Today.AddDays(-30);
                LoadReportData();
            };
        }

        private void DateRangeChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DateFromPicker.SelectedDate.HasValue && DateToPicker.SelectedDate.HasValue)
            {
                LoadReportData();
            }
        }

        private void LoadReportData()
        {
            if (!DateFromPicker.SelectedDate.HasValue || !DateToPicker.SelectedDate.HasValue)
                return;

            startDate = DateFromPicker.SelectedDate.Value;
            endDate = DateToPicker.SelectedDate.Value.AddDays(1).AddSeconds(-1);

            try
            {
                LoadKeyMetrics();
                LoadTrainerRevenueChart();
                LoadTrainerWorkloadChart();
                LoadTrainerPopularityChart();
                LoadTrainerServicesChart();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки данных: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadKeyMetrics()
        {
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();

                // Активные тренеры
                using (SqlCommand cmd = new SqlCommand(@"
                    SELECT COUNT(DISTINCT s.StaffID) 
                    FROM Staffs s
                    JOIN Roles r ON s.RoleID = r.RoleID
                    WHERE r.Name = 'Тренер'
                    AND EXISTS (
                        SELECT 1 FROM Schedules sch 
                        WHERE sch.TrainerID = s.StaffID 
                        AND sch.StartDateTime BETWEEN @StartDate AND @EndDate
                    )", connection))
                {
                    cmd.Parameters.AddWithValue("@StartDate", startDate);
                    cmd.Parameters.AddWithValue("@EndDate", endDate);
                    ActiveTrainersTextBlock.Text = cmd.ExecuteScalar()?.ToString() ?? "0";
                }

                // Всего тренировок
                using (SqlCommand cmd = new SqlCommand(@"
                    SELECT COUNT(*) 
                    FROM Schedules s
                    WHERE s.TrainerID IS NOT NULL
                    AND s.StartDateTime BETWEEN @StartDate AND @EndDate", connection))
                {
                    cmd.Parameters.AddWithValue("@StartDate", startDate);
                    cmd.Parameters.AddWithValue("@EndDate", endDate);
                    TotalTrainingsTextBlock.Text = cmd.ExecuteScalar()?.ToString() ?? "0";
                }

                // Количество клиентов тренеров
                using (SqlCommand cmd = new SqlCommand(@"
                    SELECT COUNT(DISTINCT s.ClientID)
                    FROM Schedules s
                    WHERE s.TrainerID IS NOT NULL
                    AND s.ClientID IS NOT NULL
                    AND s.StartDateTime BETWEEN @StartDate AND @EndDate", connection))
                {
                    cmd.Parameters.AddWithValue("@StartDate", startDate);
                    cmd.Parameters.AddWithValue("@EndDate", endDate);
                    TrainerClientsTextBlock.Text = cmd.ExecuteScalar()?.ToString() ?? "0";
                }

                // Выручка тренеров
                using (SqlCommand cmd = new SqlCommand(@"
                    SELECT ISNULL(SUM(s.PriceSold), 0)
                    FROM Sales s
                    WHERE s.TrainerID IS NOT NULL
                    AND s.SaleDateTime BETWEEN @StartDate AND @EndDate", connection))
                {
                    cmd.Parameters.AddWithValue("@StartDate", startDate);
                    cmd.Parameters.AddWithValue("@EndDate", endDate);
                    decimal revenue = Convert.ToDecimal(cmd.ExecuteScalar() ?? 0);
                    TrainerRevenueTextBlock.Text = $"{revenue:N0} ₽";
                }

                // Средняя стоимость
                using (SqlCommand cmd = new SqlCommand(@"
                    SELECT ISNULL(AVG(s.PriceSold), 0)
                    FROM Sales s
                    WHERE s.TrainerID IS NOT NULL
                    AND s.SaleDateTime BETWEEN @StartDate AND @EndDate", connection))
                {
                    cmd.Parameters.AddWithValue("@StartDate", startDate);
                    cmd.Parameters.AddWithValue("@EndDate", endDate);
                    decimal avgPrice = Convert.ToDecimal(cmd.ExecuteScalar() ?? 0);
                    AverageTrainingPriceTextBlock.Text = $"{avgPrice:N0} ₽";
                }

                // Лучший тренер
                using (SqlCommand cmd = new SqlCommand(@"
                    SELECT TOP 1 p.Surname + ' ' + p.Name
                    FROM Sales s
                    JOIN Staffs st ON s.TrainerID = st.StaffID
                    JOIN Persons p ON st.PersonID = p.PersonID
                    WHERE s.SaleDateTime BETWEEN @StartDate AND @EndDate
                    GROUP BY s.TrainerID, p.Surname, p.Name
                    ORDER BY SUM(s.PriceSold) DESC", connection))
                {
                    cmd.Parameters.AddWithValue("@StartDate", startDate);
                    cmd.Parameters.AddWithValue("@EndDate", endDate);
                    object result = cmd.ExecuteScalar();
                    TopTrainerTextBlock.Text = result != null ? result.ToString() : "Н/Д";
                }
            }
        }

        private void LoadTrainerRevenueChart()
        {
            SeriesCollection seriesCollection = new SeriesCollection();
            List<string> labels = new List<string>();
            ChartValues<double> values = new ChartValues<double>();

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();

                using (SqlCommand cmd = new SqlCommand(@"
                    SELECT TOP 10 p.Surname + ' ' + LEFT(p.Name, 1) + '.' as TrainerName, 
                           SUM(s.PriceSold) as TotalRevenue
                    FROM Sales s
                    JOIN Staffs st ON s.TrainerID = st.StaffID
                    JOIN Persons p ON st.PersonID = p.PersonID
                    WHERE s.SaleDateTime BETWEEN @StartDate AND @EndDate
                    GROUP BY s.TrainerID, p.Surname, p.Name
                    ORDER BY TotalRevenue DESC", connection))
                {
                    cmd.Parameters.AddWithValue("@StartDate", startDate);
                    cmd.Parameters.AddWithValue("@EndDate", endDate);

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            labels.Add(reader["TrainerName"].ToString());
                            values.Add(Convert.ToDouble(reader["TotalRevenue"]));
                        }
                    }
                }
            }

            seriesCollection.Add(new ColumnSeries
            {
                Title = "Выручка",
                Values = values,
                Fill = System.Windows.Media.Brushes.DodgerBlue
            });

            var chart = new CartesianChart
            {
                Series = seriesCollection,
                LegendLocation = LegendLocation.Top,
                DisableAnimations = true
            };

            chart.AxisX.Add(new Axis
            {
                Title = "Тренер",
                Labels = labels,
                Separator = new Separator { Step = 1 }
            });

            chart.AxisY.Add(new Axis
            {
                Title = "Выручка (₽)",
                MinValue = 0
            });

            TrainerRevenueChartContainer.Content = chart;
        }

        private void LoadTrainerWorkloadChart()
        {
            SeriesCollection seriesCollection = new SeriesCollection();
            List<string> labels = new List<string>();
            ChartValues<double> values = new ChartValues<double>();

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();

                using (SqlCommand cmd = new SqlCommand(@"
                    SELECT TOP 10 p.Surname + ' ' + LEFT(p.Name, 1) + '.' as TrainerName, 
                           COUNT(*) as TrainingCount
                    FROM Schedules s
                    JOIN Staffs st ON s.TrainerID = st.StaffID
                    JOIN Persons p ON st.PersonID = p.PersonID
                    WHERE s.StartDateTime BETWEEN @StartDate AND @EndDate
                    GROUP BY s.TrainerID, p.Surname, p.Name
                    ORDER BY TrainingCount DESC", connection))
                {
                    cmd.Parameters.AddWithValue("@StartDate", startDate);
                    cmd.Parameters.AddWithValue("@EndDate", endDate);

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            labels.Add(reader["TrainerName"].ToString());
                            values.Add(Convert.ToDouble(reader["TrainingCount"]));
                        }
                    }
                }
            }

            seriesCollection.Add(new ColumnSeries
            {
                Title = "Тренировки",
                Values = values,
                Fill = System.Windows.Media.Brushes.MediumSeaGreen
            });

            var chart = new CartesianChart
            {
                Series = seriesCollection,
                LegendLocation = LegendLocation.Top,
                DisableAnimations = true
            };

            chart.AxisX.Add(new Axis
            {
                Title = "Тренер",
                Labels = labels,
                Separator = new Separator { Step = 1 }
            });

            chart.AxisY.Add(new Axis
            {
                Title = "Количество тренировок",
                MinValue = 0
            });

            TrainerWorkloadChartContainer.Content = chart;
        }

        private void LoadTrainerPopularityChart()
        {
            SeriesCollection seriesCollection = new SeriesCollection();
            List<string> labels = new List<string>();
            ChartValues<double> values = new ChartValues<double>();

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();

                using (SqlCommand cmd = new SqlCommand(@"
                    SELECT TOP 10 p.Surname + ' ' + LEFT(p.Name, 1) + '.' as TrainerName, 
                           COUNT(DISTINCT s.ClientID) as ClientCount
                    FROM Schedules s
                    JOIN Staffs st ON s.TrainerID = st.StaffID
                    JOIN Persons p ON st.PersonID = p.PersonID
                    WHERE s.StartDateTime BETWEEN @StartDate AND @EndDate
                    AND s.ClientID IS NOT NULL
                    GROUP BY s.TrainerID, p.Surname, p.Name
                    ORDER BY ClientCount DESC", connection))
                {
                    cmd.Parameters.AddWithValue("@StartDate", startDate);
                    cmd.Parameters.AddWithValue("@EndDate", endDate);

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            labels.Add(reader["TrainerName"].ToString());
                            values.Add(Convert.ToDouble(reader["ClientCount"]));
                        }
                    }
                }
            }

            seriesCollection.Add(new ColumnSeries
            {
                Title = "Клиенты",
                Values = values,
                Fill = System.Windows.Media.Brushes.Coral
            });

            var chart = new CartesianChart
            {
                Series = seriesCollection,
                LegendLocation = LegendLocation.Top,
                DisableAnimations = true
            };

            chart.AxisX.Add(new Axis
            {
                Title = "Тренер",
                Labels = labels,
                Separator = new Separator { Step = 1 }
            });

            chart.AxisY.Add(new Axis
            {
                Title = "Количество клиентов",
                MinValue = 0
            });

            TrainerPopularityChartContainer.Content = chart;
        }

        private void LoadTrainerServicesChart()
        {
            SeriesCollection seriesCollection = new SeriesCollection();

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();

                using (SqlCommand cmd = new SqlCommand(@"
                    SELECT TOP 5 ser.Name as ServiceName, COUNT(*) as ServiceCount
                    FROM ServiceTrainer st
                    JOIN Services ser ON st.ServiceID = ser.ServiceID
                    JOIN Staffs s ON st.TrainerID = s.StaffID
                    JOIN Schedules sch ON s.StaffID = sch.TrainerID
                    WHERE sch.StartDateTime BETWEEN @StartDate AND @EndDate
                    GROUP BY ser.ServiceID, ser.Name
                    ORDER BY ServiceCount DESC", connection))
                {
                    cmd.Parameters.AddWithValue("@StartDate", startDate);
                    cmd.Parameters.AddWithValue("@EndDate", endDate);

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            seriesCollection.Add(new PieSeries
                            {
                                Title = reader["ServiceName"].ToString(),
                                Values = new ChartValues<double> { Convert.ToDouble(reader["ServiceCount"]) },
                                DataLabels = true
                            });
                        }
                    }
                }
            }

            var chart = new PieChart
            {
                Series = seriesCollection,
                LegendLocation = LegendLocation.Right,
                DisableAnimations = true
            };

            TrainerServicesChartContainer.Content = chart;
        }

        private void ExportToWord_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var saveFileDialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "Word Documents (*.docx)|*.docx",
                    DefaultExt = "docx"
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    string filePath = saveFileDialog.FileName;
                    ExportToWordDocument(filePath);
                    MessageBox.Show("Отчет успешно экспортирован в Word", "Экспорт", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при экспорте: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExportToWordDocument(string filePath)
        {
            using (WordprocessingDocument wordDocument =
                WordprocessingDocument.Create(filePath, WordprocessingDocumentType.Document))
            {
                // Основная часть документа
                MainDocumentPart mainPart = wordDocument.AddMainDocumentPart();
                mainPart.Document = new Document();

                // Настройка страницы для содержимого
                Body body = mainPart.Document.AppendChild(new Body());

                // Настройка полей страницы для лучшего размещения таблиц
                SectionProperties sectionProperties = new SectionProperties();
                sectionProperties.AppendChild(new PageSize() { Width = 11900, Height = 16840 }); // А4
                sectionProperties.AppendChild(new PageMargin() { Top = 720, Right = 720, Bottom = 720, Left = 720 }); // 0.5 inch margins
                body.AppendChild(sectionProperties);

                // Главный заголовок документа
                Paragraph titleParagraph = new Paragraph(
                    new ParagraphProperties(
                        new Justification() { Val = JustificationValues.Center },
                        new SpacingBetweenLines() { After = "0", Before = "0" }
                    ),
                    new Run(
                        new RunProperties(
                            new Bold(),
                            new FontSize() { Val = "36" } // 18pt
                        ),
                        new Text("ОТЧЕТ")
                    )
                );
                body.AppendChild(titleParagraph);

                // Подзаголовок с описанием отчета
                Paragraph subTitleParagraph = new Paragraph(
                    new ParagraphProperties(
                        new Justification() { Val = JustificationValues.Center },
                        new SpacingBetweenLines() { After = "400", Before = "0" }
                    ),
                    new Run(
                        new RunProperties(
                            new FontSize() { Val = "28" } // 14pt
                        ),
                        new Text($"по тренерам за период {DateFromPicker.SelectedDate?.ToString("dd.MM.yyyy")} - {DateToPicker.SelectedDate?.ToString("dd.MM.yyyy")}")
                    )
                );
                body.AppendChild(subTitleParagraph);

                // Информация о формировании отчета
                Paragraph dateInfo = new Paragraph(
                    new Run(
                        new RunProperties(new Bold()),
                        new Text($"Дата формирования: {DateTime.Now:dd.MM.yyyy}")
                    )
                );
                body.AppendChild(dateInfo);

                // Добавляем разделитель перед содержимым
                Paragraph divider = new Paragraph(
                    new ParagraphProperties(
                        new ParagraphBorders(
                            new BottomBorder() { Val = BorderValues.Single, Size = 6, Space = 1, Color = "AAAAAA" }
                        ),
                        new SpacingBetweenLines() { After = "400", Before = "200" }
                    )
                );
                body.AppendChild(divider);

                // Секция с ключевыми показателями
                Paragraph metricsHeader = new Paragraph(
                    new Run(
                        new RunProperties(
                            new Bold(),
                            new FontSize() { Val = "28" }, // 14pt
                            new Color() { Val = "2F5496" }
                        ),
                        new Text("1. Ключевые показатели")
                    )
                );
                body.AppendChild(metricsHeader);

                // Таблица с ключевыми показателями
                Table metricsTable = CreateMetricsTable();
                body.AppendChild(metricsTable);

                // Раздел с выручкой тренеров
                Paragraph revenueHeader = new Paragraph(
                    new Run(
                        new RunProperties(
                            new Bold(),
                            new FontSize() { Val = "28" }, // 14pt
                            new Color() { Val = "2F5496" }
                        ),
                        new Text("2. Выручка тренеров")
                    )
                );
                body.AppendChild(revenueHeader);

                // Таблица с выручкой тренеров
                Table revenueTable = CreateTrainerRevenueTable();
                body.AppendChild(revenueTable);

                // Раздел с загруженностью тренеров
                Paragraph workloadHeader = new Paragraph(
                    new Run(
                        new RunProperties(
                            new Bold(),
                            new FontSize() { Val = "28" }, // 14pt
                            new Color() { Val = "2F5496" }
                        ),
                        new Text("3. Загруженность тренеров")
                    )
                );
                body.AppendChild(workloadHeader);

                // Таблица с загруженностью тренеров
                Table workloadTable = CreateTrainerWorkloadTable();
                body.AppendChild(workloadTable);

                // Раздел с популярностью тренеров
                Paragraph popularityHeader = new Paragraph(
                    new Run(
                        new RunProperties(
                            new Bold(),
                            new FontSize() { Val = "28" }, // 14pt
                            new Color() { Val = "2F5496" }
                        ),
                        new Text("4. Популярность тренеров")
                    )
                );
                body.AppendChild(popularityHeader);

                // Таблица с популярностью тренеров
                Table popularityTable = CreateTrainerPopularityTable();
                body.AppendChild(popularityTable);

                // Раздел с услугами тренеров
                Paragraph servicesHeader = new Paragraph(
                    new Run(
                        new RunProperties(
                            new Bold(),
                            new FontSize() { Val = "28" }, // 14pt
                            new Color() { Val = "2F5496" }
                        ),
                        new Text("5. Распределение услуг тренеров")
                    )
                );
                body.AppendChild(servicesHeader);

                // Таблица с услугами тренеров
                Table servicesTable = CreateTrainerServicesTable();
                body.AppendChild(servicesTable);
            }
        }

        private Table CreateMetricsTable()
        {
            Table table = new Table();

            // Настраиваем таблицу на 100% ширины страницы
            TableProperties tblProp = new TableProperties(
                new TableWidth() { Width = "5000", Type = TableWidthUnitValues.Pct },
                new TableJustification() { Val = TableRowAlignmentValues.Center },
                new TableBorders(
                    new TopBorder() { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 4, Color = "000000" },
                    new BottomBorder() { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 4, Color = "000000" },
                    new LeftBorder() { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 4, Color = "000000" },
                    new RightBorder() { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 4, Color = "000000" },
                    new InsideHorizontalBorder() { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 2, Color = "000000" },
                    new InsideVerticalBorder() { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 2, Color = "000000" }
                ),
                new TableLook()
                {
                    Val = new HexBinaryValue() { Value = "04A0" },
                    FirstRow = true,
                    LastRow = false,
                    FirstColumn = true,
                    LastColumn = false,
                    NoHorizontalBand = false,
                    NoVerticalBand = true
                }
            );
            table.AppendChild(tblProp);

            // Настраиваем ширину столбцов: первый шире, второй уже
            TableGrid tableGrid = new TableGrid(
                new GridColumn() { Width = "7000" },
                new GridColumn() { Width = "3000" }
            );
            table.AppendChild(tableGrid);

            // Создаем заголовок таблицы
            TableRow headerRow = new TableRow();
            headerRow.AppendChild(CreateHeaderCell("Показатель"));
            headerRow.AppendChild(CreateHeaderCell("Значение"));

            // Задаем высоту строки заголовка
            TableRowProperties headerRowProperties = new TableRowProperties();
            headerRowProperties.AppendChild(new TableRowHeight() { Val = 400, HeightType = HeightRuleValues.AtLeast });
            headerRow.AppendChild(headerRowProperties);

            table.AppendChild(headerRow);

            // Добавляем строки данных
            table.AppendChild(CreateDataRow("Активные тренеры", ActiveTrainersTextBlock.Text, false));
            table.AppendChild(CreateDataRow("Количество тренировок", TotalTrainingsTextBlock.Text, true));
            table.AppendChild(CreateDataRow("Количество клиентов", TrainerClientsTextBlock.Text, false));
            table.AppendChild(CreateDataRow("Выручка", TrainerRevenueTextBlock.Text, true));
            table.AppendChild(CreateDataRow("Средняя стоимость тренировки", AverageTrainingPriceTextBlock.Text, false));
            table.AppendChild(CreateDataRow("Лучший тренер", TopTrainerTextBlock.Text, true));

            return table;
        }

        private Table CreateTrainerRevenueTable()
        {
            Table table = new Table();

            // Настраиваем таблицу на 100% ширины страницы
            TableProperties tblProp = new TableProperties(
                new TableWidth() { Width = "5000", Type = TableWidthUnitValues.Pct },
                new TableJustification() { Val = TableRowAlignmentValues.Center },
                new TableBorders(
                    new TopBorder() { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 4, Color = "000000" },
                    new BottomBorder() { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 4, Color = "000000" },
                    new LeftBorder() { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 4, Color = "000000" },
                    new RightBorder() { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 4, Color = "000000" },
                    new InsideHorizontalBorder() { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 2, Color = "000000" },
                    new InsideVerticalBorder() { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 2, Color = "000000" }
                ),
                new TableLook()
                {
                    Val = new HexBinaryValue() { Value = "04A0" },
                    FirstRow = true,
                    LastRow = false,
                    FirstColumn = true,
                    LastColumn = false,
                    NoHorizontalBand = false,
                    NoVerticalBand = true
                }
            );
            table.AppendChild(tblProp);

            // Настраиваем ширину столбцов
            TableGrid tableGrid = new TableGrid(
                new GridColumn() { Width = "7000" },
                new GridColumn() { Width = "3000" }
            );
            table.AppendChild(tableGrid);

            // Создаем заголовок таблицы
            TableRow headerRow = new TableRow();
            headerRow.AppendChild(CreateHeaderCell("Тренер"));
            headerRow.AppendChild(CreateHeaderCell("Выручка (₽)"));

            // Задаем высоту строки заголовка
            TableRowProperties headerRowProperties = new TableRowProperties();
            headerRowProperties.AppendChild(new TableRowHeight() { Val = 400, HeightType = HeightRuleValues.AtLeast });
            headerRow.AppendChild(headerRowProperties);

            table.AppendChild(headerRow);

            // Данные о выручке тренеров
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();

                using (SqlCommand cmd = new SqlCommand(@"
                    SELECT TOP 10 p.Surname + ' ' + p.Name as TrainerName, 
                           SUM(s.PriceSold) as TotalRevenue
                    FROM Sales s
                    JOIN Staffs st ON s.TrainerID = st.StaffID
                    JOIN Persons p ON st.PersonID = p.PersonID
                    WHERE s.SaleDateTime BETWEEN @StartDate AND @EndDate
                    GROUP BY s.TrainerID, p.Surname, p.Name
                    ORDER BY TotalRevenue DESC", connection))
                {
                    cmd.Parameters.AddWithValue("@StartDate", startDate);
                    cmd.Parameters.AddWithValue("@EndDate", endDate);

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        bool isAlternateRow = false;
                        while (reader.Read())
                        {
                            isAlternateRow = !isAlternateRow;
                            decimal revenue = Convert.ToDecimal(reader["TotalRevenue"]);
                            TableRow dataRow = CreateDataRow(
                                reader["TrainerName"].ToString(),
                                $"{revenue:N0} ₽",
                                isAlternateRow);

                            table.AppendChild(dataRow);
                        }
                    }
                }
            }

            return table;
        }

        private Table CreateTrainerWorkloadTable()
        {
            Table table = new Table();

            // Настраиваем таблицу на 100% ширины страницы
            TableProperties tblProp = new TableProperties(
                new TableWidth() { Width = "5000", Type = TableWidthUnitValues.Pct },
                new TableJustification() { Val = TableRowAlignmentValues.Center },
                new TableBorders(
                    new TopBorder() { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 4, Color = "000000" },
                    new BottomBorder() { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 4, Color = "000000" },
                    new LeftBorder() { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 4, Color = "000000" },
                    new RightBorder() { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 4, Color = "000000" },
                    new InsideHorizontalBorder() { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 2, Color = "000000" },
                    new InsideVerticalBorder() { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 2, Color = "000000" }
                ),
                new TableLook()
                {
                    Val = new HexBinaryValue() { Value = "04A0" },
                    FirstRow = true,
                    LastRow = false,
                    FirstColumn = true,
                    LastColumn = false,
                    NoHorizontalBand = false,
                    NoVerticalBand = true
                }
            );
            table.AppendChild(tblProp);

            // Настраиваем ширину столбцов
            TableGrid tableGrid = new TableGrid(
                new GridColumn() { Width = "7000" },
                new GridColumn() { Width = "3000" }
            );
            table.AppendChild(tableGrid);

            // Создаем заголовок таблицы
            TableRow headerRow = new TableRow();
            headerRow.AppendChild(CreateHeaderCell("Тренер"));
            headerRow.AppendChild(CreateHeaderCell("Количество тренировок"));

            // Задаем высоту строки заголовка
            TableRowProperties headerRowProperties = new TableRowProperties();
            headerRowProperties.AppendChild(new TableRowHeight() { Val = 400, HeightType = HeightRuleValues.AtLeast });
            headerRow.AppendChild(headerRowProperties);

            table.AppendChild(headerRow);

            // Данные о загруженности тренеров
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();

                using (SqlCommand cmd = new SqlCommand(@"
                    SELECT TOP 10 p.Surname + ' ' + p.Name as TrainerName, 
                           COUNT(*) as TrainingCount
                    FROM Schedules s
                    JOIN Staffs st ON s.TrainerID = st.StaffID
                    JOIN Persons p ON st.PersonID = p.PersonID
                    WHERE s.StartDateTime BETWEEN @StartDate AND @EndDate
                    GROUP BY s.TrainerID, p.Surname, p.Name
                    ORDER BY TrainingCount DESC", connection))
                {
                    cmd.Parameters.AddWithValue("@StartDate", startDate);
                    cmd.Parameters.AddWithValue("@EndDate", endDate);

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        bool isAlternateRow = false;
                        while (reader.Read())
                        {
                            isAlternateRow = !isAlternateRow;
                            TableRow dataRow = CreateDataRow(
                                reader["TrainerName"].ToString(),
                                reader["TrainingCount"].ToString(),
                                isAlternateRow);

                            table.AppendChild(dataRow);
                        }
                    }
                }
            }

            return table;
        }

        private Table CreateTrainerPopularityTable()
        {
            Table table = new Table();

            // Настраиваем таблицу на 100% ширины страницы
            TableProperties tblProp = new TableProperties(
                new TableWidth() { Width = "5000", Type = TableWidthUnitValues.Pct },
                new TableJustification() { Val = TableRowAlignmentValues.Center },
                new TableBorders(
                    new TopBorder() { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 4, Color = "000000" },
                    new BottomBorder() { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 4, Color = "000000" },
                    new LeftBorder() { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 4, Color = "000000" },
                    new RightBorder() { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 4, Color = "000000" },
                    new InsideHorizontalBorder() { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 2, Color = "000000" },
                    new InsideVerticalBorder() { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 2, Color = "000000" }
                ),
                new TableLook()
                {
                    Val = new HexBinaryValue() { Value = "04A0" },
                    FirstRow = true,
                    LastRow = false,
                    FirstColumn = true,
                    LastColumn = false,
                    NoHorizontalBand = false,
                    NoVerticalBand = true
                }
            );
            table.AppendChild(tblProp);

            // Настраиваем ширину столбцов
            TableGrid tableGrid = new TableGrid(
                new GridColumn() { Width = "7000" },
                new GridColumn() { Width = "3000" }
            );
            table.AppendChild(tableGrid);

            // Создаем заголовок таблицы
            TableRow headerRow = new TableRow();
            headerRow.AppendChild(CreateHeaderCell("Тренер"));
            headerRow.AppendChild(CreateHeaderCell("Количество клиентов"));

            // Задаем высоту строки заголовка
            TableRowProperties headerRowProperties = new TableRowProperties();
            headerRowProperties.AppendChild(new TableRowHeight() { Val = 400, HeightType = HeightRuleValues.AtLeast });
            headerRow.AppendChild(headerRowProperties);

            table.AppendChild(headerRow);

            // Данные о популярности тренеров
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();

                using (SqlCommand cmd = new SqlCommand(@"
                    SELECT TOP 10 p.Surname + ' ' + p.Name as TrainerName, 
                           COUNT(DISTINCT s.ClientID) as ClientCount
                    FROM Schedules s
                    JOIN Staffs st ON s.TrainerID = st.StaffID
                    JOIN Persons p ON st.PersonID = p.PersonID
                    WHERE s.StartDateTime BETWEEN @StartDate AND @EndDate
                    AND s.ClientID IS NOT NULL
                    GROUP BY s.TrainerID, p.Surname, p.Name
                    ORDER BY ClientCount DESC", connection))
                {
                    cmd.Parameters.AddWithValue("@StartDate", startDate);
                    cmd.Parameters.AddWithValue("@EndDate", endDate);

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        bool isAlternateRow = false;
                        while (reader.Read())
                        {
                            isAlternateRow = !isAlternateRow;
                            TableRow dataRow = CreateDataRow(
                                reader["TrainerName"].ToString(),
                                reader["ClientCount"].ToString(),
                                isAlternateRow);

                            table.AppendChild(dataRow);
                        }
                    }
                }
            }

            return table;
        }

        private Table CreateTrainerServicesTable()
        {
            Table table = new Table();

            // Настраиваем таблицу на 100% ширины страницы
            TableProperties tblProp = new TableProperties(
                new TableWidth() { Width = "5000", Type = TableWidthUnitValues.Pct },
                new TableJustification() { Val = TableRowAlignmentValues.Center },
                new TableBorders(
                    new TopBorder() { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 4, Color = "000000" },
                    new BottomBorder() { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 4, Color = "000000" },
                    new LeftBorder() { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 4, Color = "000000" },
                    new RightBorder() { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 4, Color = "000000" },
                    new InsideHorizontalBorder() { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 2, Color = "000000" },
                    new InsideVerticalBorder() { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 2, Color = "000000" }
                ),
                new TableLook()
                {
                    Val = new HexBinaryValue() { Value = "04A0" },
                    FirstRow = true,
                    LastRow = false,
                    FirstColumn = true,
                    LastColumn = false,
                    NoHorizontalBand = false,
                    NoVerticalBand = true
                }
            );
            table.AppendChild(tblProp);

            // Настраиваем ширину столбцов
            TableGrid tableGrid = new TableGrid(
                new GridColumn() { Width = "7000" },
                new GridColumn() { Width = "3000" }
            );
            table.AppendChild(tableGrid);

            // Создаем заголовок таблицы
            TableRow headerRow = new TableRow();
            headerRow.AppendChild(CreateHeaderCell("Услуга"));
            headerRow.AppendChild(CreateHeaderCell("Количество"));

            // Задаем высоту строки заголовка
            TableRowProperties headerRowProperties = new TableRowProperties();
            headerRowProperties.AppendChild(new TableRowHeight() { Val = 400, HeightType = HeightRuleValues.AtLeast });
            headerRow.AppendChild(headerRowProperties);

            table.AppendChild(headerRow);

            // Данные о распределении услуг
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();

                using (SqlCommand cmd = new SqlCommand(@"
                    SELECT TOP 10 ser.Name as ServiceName, COUNT(*) as ServiceCount
                    FROM ServiceTrainer st
                    JOIN Services ser ON st.ServiceID = ser.ServiceID
                    JOIN Staffs s ON st.TrainerID = s.StaffID
                    JOIN Schedules sch ON s.StaffID = sch.TrainerID
                    WHERE sch.StartDateTime BETWEEN @StartDate AND @EndDate
                    GROUP BY ser.ServiceID, ser.Name
                    ORDER BY ServiceCount DESC", connection))
                {
                    cmd.Parameters.AddWithValue("@StartDate", startDate);
                    cmd.Parameters.AddWithValue("@EndDate", endDate);

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        bool isAlternateRow = false;
                        bool hasData = false;
                        while (reader.Read())
                        {
                            hasData = true;
                            isAlternateRow = !isAlternateRow;
                            TableRow dataRow = CreateDataRow(
                                reader["ServiceName"].ToString(),
                                reader["ServiceCount"].ToString(),
                                isAlternateRow);

                            table.AppendChild(dataRow);
                        }

                        // Если данных нет, добавляем пустую строку
                        if (!hasData)
                        {
                            TableRow noDataRow = CreateDataRow("Нет данных", "0", false);
                            table.AppendChild(noDataRow);
                        }
                    }
                }
            }

            return table;
        }

        private TableCell CreateHeaderCell(string text)
        {
            TableCell cell = new TableCell();

            // Настраиваем параметры ячейки заголовка
            TableCellProperties cellProperties = new TableCellProperties(
                new TableCellWidth() { Type = TableWidthUnitValues.Auto },
                new Shading()
                {
                    Val = ShadingPatternValues.Clear,
                    Color = "auto",
                    Fill = "DEDEDE" // Более темный серый фон для лучшего контраста
                },
                new TableCellVerticalAlignment() { Val = TableVerticalAlignmentValues.Center },
                new TableCellMargin()
                {
                    TopMargin = new TopMargin() { Width = "60" },
                    BottomMargin = new BottomMargin() { Width = "60" },
                    LeftMargin = new LeftMargin() { Width = "60" },
                    RightMargin = new RightMargin() { Width = "60" }
                }
            );
            cell.AppendChild(cellProperties);

            // Создаем параграф с центрированным текстом заголовка
            Paragraph paragraph = new Paragraph(
                new ParagraphProperties(
                    new Justification() { Val = JustificationValues.Center },
                    new SpacingBetweenLines() { Before = "0", After = "0" }
                ),
                new Run(
                    new RunProperties(
                        new Bold(),
                        new FontSize() { Val = "22" }, // 11pt
                        new Color() { Val = "000000" }
                    ),
                    new Text(text)
                )
            );

            cell.AppendChild(paragraph);

            return cell;
        }

        private TableCell CreateDataCell(string text, bool alignRight = false, bool isAlternateRow = false)
        {
            TableCell cell = new TableCell();

            // Параметры ячейки данных, включая альтернативное выделение строк для четкости
            TableCellProperties cellProperties = new TableCellProperties(
                new TableCellWidth() { Type = TableWidthUnitValues.Auto },
                new TableCellVerticalAlignment() { Val = TableVerticalAlignmentValues.Center },
                new TableCellMargin()
                {
                    TopMargin = new TopMargin() { Width = "60" },
                    BottomMargin = new BottomMargin() { Width = "60" },
                    LeftMargin = new LeftMargin() { Width = "120" }, // Больший отступ слева для лучшей читаемости
                    RightMargin = new RightMargin() { Width = "120" }
                }
            );

            // Если это альтернативная строка, добавляем легкое затенение
            if (isAlternateRow)
            {
                cellProperties.AppendChild(new Shading()
                {
                    Val = ShadingPatternValues.Clear,
                    Color = "auto",
                    Fill = "F9F9F9" // Очень светлый фон для альтернативных строк
                });
            }

            cell.AppendChild(cellProperties);

            // Параграф с текстом, выравнивание зависит от типа данных
            Paragraph paragraph = new Paragraph(
                new ParagraphProperties(
                    new Justification() { Val = alignRight ? JustificationValues.Right : JustificationValues.Left },
                    new SpacingBetweenLines() { Before = "0", After = "0" }
                ),
                new Run(
                    new RunProperties(
                        new FontSize() { Val = "22" } // 11pt
                    ),
                    new Text(text)
                )
            );

            cell.AppendChild(paragraph);

            return cell;
        }

        private TableRow CreateDataRow(string label, string value, bool isAlternateRow = false)
        {
            TableRow row = new TableRow();

            // Настройки высоты строки для лучшей презентации
            TableRowProperties rowProperties = new TableRowProperties();
            rowProperties.AppendChild(new TableRowHeight() { Val = 360, HeightType = HeightRuleValues.AtLeast });
            row.AppendChild(rowProperties);

            // Добавляем ячейки с учетом альтернативного выделения строк
            row.AppendChild(CreateDataCell(label, false, isAlternateRow));
            row.AppendChild(CreateDataCell(value, true, isAlternateRow));

            return row;
        }
    }
}