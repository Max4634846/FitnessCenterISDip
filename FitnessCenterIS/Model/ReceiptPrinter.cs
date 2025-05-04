using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Xps;
using System.Windows.Xps.Packaging;
using System.IO;
using System.IO.Packaging;
using FitnessCenterIS.Model;
using FitnessCenterIS.View.Windows;
using System.Windows.Media.Imaging;

namespace FitnessCenterIS.Model
{
    public class ReceiptPrinter
    {
        public static void PrintReceipt(Sales sale, PaymentResult paymentResult)
        {
            try
            {
                // Создаем документ для печати
                FlowDocument document = CreateReceiptDocument(sale, paymentResult);

                // Создаем диалог печати
                System.Windows.Controls.PrintDialog printDialog = new System.Windows.Controls.PrintDialog();

                // Если пользователь нажал OK, печатаем документ
                if (printDialog.ShowDialog() == true)
                {
                    // Установка параметров печати
                    document.PageHeight = printDialog.PrintableAreaHeight;
                    document.PageWidth = printDialog.PrintableAreaWidth;
                    document.PagePadding = new Thickness(50);
                    document.ColumnGap = 0;
                    document.ColumnWidth = printDialog.PrintableAreaWidth;

                    // Создаем пагинатор
                    IDocumentPaginatorSource paginatorSource = document;
                    printDialog.PrintDocument(paginatorSource.DocumentPaginator, "Чек на оплату");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при печати чека: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Метод для предварительного просмотра чека
        public static void ShowReceiptPreview(Sales sale, PaymentResult paymentResult)
        {
            try
            {
                // Создаем документ
                FlowDocument document = CreateReceiptDocument(sale, paymentResult);

                // Создаем окно предварительного просмотра
                ReceiptPreviewWindow previewWindow = new ReceiptPreviewWindow(document);
                previewWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при предварительном просмотре чека: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static FlowDocument CreateReceiptDocument(Sales sale, PaymentResult paymentResult)
        {
            using (var context = new BDFitnessClubDipEntities())
            {
                // Загружаем всю необходимую информацию
                var saleData = context.Sales
                    .Include("Seasontickets")
                    .Include("Staffs")
                    .Include("Staffs.Persons")
                    .Include("Vatrates")
                    .FirstOrDefault(s => s.SaleID == sale.SaleID);

                if (saleData == null)
                    throw new Exception("Продажа не найдена");

                // ИСПРАВЛЕНИЕ: Получаем реального администратора, который оформил продажу
                string adminName = string.Empty;
                if (saleData.AdministratorID.HasValue)
                {
                    var admin = context.Users
                        .Include("Staffs.Persons")
                        .FirstOrDefault(u => u.UserID == saleData.AdministratorID);

                    if (admin != null && admin.Staffs != null && admin.Staffs.Persons != null)
                    {
                        adminName = $"{admin.Staffs.Persons.Surname} {admin.Staffs.Persons.Name}";
                    }
                }

                // Получаем имя клиента
                string clientName = string.Empty;
                var client = context.SeasonticketClients
                    .Where(sc => sc.SeasonticketID == saleData.SeasonticketID)
                    .Select(sc => sc.Clients.Persons)
                    .FirstOrDefault();

                if (client != null)
                {
                    clientName = $"{client.Surname} {client.Name} {client.MiddleName}";
                }
                else
                {
                    // Если клиент не найден через сезонный абонемент (например, это разовая услуга)
                    // то ищем по другим связям
                    var clientServices = context.Schedules
                        .Where(sch => sch.SeasonticketServiceID == saleData.SeasonticketServiceID)
                        .Select(sch => sch.Clients.Persons)
                        .FirstOrDefault();

                    if (clientServices != null)
                    {
                        clientName = $"{clientServices.Surname} {clientServices.Name} {clientServices.MiddleName}";
                    }
                }

                // Название абонемента/услуги
                string serviceName = string.Empty;
                if (saleData.Seasontickets != null)
                {
                    serviceName = saleData.Seasontickets.Name;
                }
                else if (saleData.SeasonticketServiceID.HasValue)
                {
                    var service = context.SeasonticketServices
                        .Where(ss => ss.SeasonticketServiceID == saleData.SeasonticketServiceID)
                        .Select(ss => ss.Services)
                        .FirstOrDefault();

                    if (service != null)
                    {
                        serviceName = service.Name;
                    }
                }

                // Тренер - используем прямой запрос для поиска тренера
                string trainerName = string.Empty;
                if (saleData.TrainerID.HasValue)
                {
                    var trainer = context.Staffs
                        .Include("Persons")
                        .FirstOrDefault(s => s.StaffID == saleData.TrainerID);

                    if (trainer != null && trainer.Persons != null)
                    {
                        trainerName = $"{trainer.Persons.Surname} {trainer.Persons.Name}";
                    }
                }

                // Создаем документ
                FlowDocument document = new FlowDocument();
                document.FontFamily = new FontFamily("Arial");
                document.FontSize = 12;

                // Заголовок
                Paragraph header = new Paragraph(new Run("ФИТНЕС-ЦЕНТР \"ФИТНЕС КЛУБ\""));
                header.FontSize = 16;
                header.FontWeight = FontWeights.Bold;
                header.TextAlignment = TextAlignment.Center;
                document.Blocks.Add(header);

                // Подзаголовок - тип документа
                Paragraph subHeader = new Paragraph(new Run("КВИТАНЦИЯ ОБ ОПЛАТЕ"));
                subHeader.FontSize = 14;
                subHeader.TextAlignment = TextAlignment.Center;
                subHeader.Margin = new Thickness(0, 5, 0, 10);
                document.Blocks.Add(subHeader);

                // Информация о продаже
                document.Blocks.Add(CreateInfoLine("№ чека:", sale.SaleID.ToString()));
                document.Blocks.Add(CreateInfoLine("Дата:", sale.SaleDateTime.ToString()));
                document.Blocks.Add(CreateInfoLine("Клиент:", clientName));

                // Добавляем разделитель
                document.Blocks.Add(CreateSeparator());

                // Информация о приобретенных услугах/абонементах
                Paragraph servicesHeader = new Paragraph(new Run("ПРИОБРЕТЕННЫЕ УСЛУГИ:"));
                servicesHeader.FontWeight = FontWeights.Bold;
                document.Blocks.Add(servicesHeader);

                document.Blocks.Add(CreateServiceLine(serviceName, saleData.PriceSold ?? 0));

                // Скидка
                if (saleData.DiscountAmount.HasValue && saleData.DiscountAmount > 0)
                {
                    document.Blocks.Add(CreateInfoLine("Скидка:", $"{saleData.DiscountAmount:N2} ₽"));
                }

                // Итого
                Paragraph totalAmount = new Paragraph(new Run($"ИТОГО К ОПЛАТЕ: {saleData.PriceSold:N2} ₽"));
                totalAmount.FontWeight = FontWeights.Bold;
                document.Blocks.Add(totalAmount);

                // Добавляем разделитель
                document.Blocks.Add(CreateSeparator());

                // Информация об оплате
                Paragraph paymentHeader = new Paragraph(new Run("ИНФОРМАЦИЯ ОБ ОПЛАТЕ:"));
                paymentHeader.FontWeight = FontWeights.Bold;
                document.Blocks.Add(paymentHeader);

                // Способ оплаты
                if (paymentResult.CardAmount > 0)
                {
                    document.Blocks.Add(CreateInfoLine("Оплата картой:", $"{paymentResult.CardAmount:N2} ₽"));
                }

                if (paymentResult.DepositAmount > 0)
                {
                    document.Blocks.Add(CreateInfoLine("Оплата с депозита:", $"{paymentResult.DepositAmount:N2} ₽"));
                }

                if (paymentResult.BonusAmount > 0)
                {
                    document.Blocks.Add(CreateInfoLine("Оплата бонусами:", $"{paymentResult.BonusAmount:N2}"));
                }

                // НДС
                if (saleData.Vatrates != null)
                {
                    decimal vatAmount = (saleData.PriceSold ?? 0) * (saleData.Vatrates.Rate ?? 0) / 100;
                    document.Blocks.Add(CreateInfoLine("В том числе НДС:", $"{vatAmount:N2} ₽ ({saleData.Vatrates.Rate}%)"));
                }

                // Информация о начисленных бонусах
                decimal addedBonus = CalculateBonus(saleData, paymentResult.BonusAmount);
                if (addedBonus > 0)
                {
                    document.Blocks.Add(CreateInfoLine("Начислено бонусов:", $"{addedBonus:N2}"));
                }

                // Добавляем разделитель
                document.Blocks.Add(CreateSeparator());

                // Дополнительная информация
                document.Blocks.Add(CreateInfoLine("Администратор:", adminName));

                if (!string.IsNullOrEmpty(trainerName))
                {
                    document.Blocks.Add(CreateInfoLine("Тренер:", trainerName));
                }

                if (saleData.StartDateTime.HasValue && saleData.EndDateTime.HasValue)
                {
                    document.Blocks.Add(CreateInfoLine("Срок действия:",
                        $"{saleData.StartDateTime.Value.ToString("dd.MM.yyyy")} - {saleData.EndDateTime.Value.ToString("dd.MM.yyyy")}"));
                }

                if (saleData.RemainingVisits.HasValue)
                {
                    document.Blocks.Add(CreateInfoLine("Оставшиеся посещения:", saleData.RemainingVisits.Value.ToString()));
                }

                // Добавляем место для печати и подписей
                Paragraph stampAndSignatures = new Paragraph();
                stampAndSignatures.Margin = new Thickness(0, 20, 0, 20);

                // Добавляем круглую печать как изображение
                System.Windows.Controls.Image stampImage = new System.Windows.Controls.Image();
                stampImage.Width = 150;
                stampImage.Height = 150;

                // Создаем круглую печать программно
                DrawingVisual drawingVisual = new DrawingVisual();
                using (DrawingContext drawingContext = drawingVisual.RenderOpen())
                {
                    // Рисуем круг
                    drawingContext.DrawEllipse(
                        Brushes.Transparent,
                        new Pen(Brushes.Black, 2),
                        new Point(75, 75),
                        70, 70);

                    // Добавляем текст "FITNESS"
                    FormattedText text = new FormattedText(
                        "FITNESS",
                        System.Globalization.CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight,
                        new Typeface("Arial Bold"),
                        20,
                        Brushes.Black);

                    drawingContext.DrawText(text, new Point(35, 65));

                    // Добавляем текст "Директор"
                    FormattedText directorText = new FormattedText(
                        "Директор: Иванов И.И.",
                        System.Globalization.CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight,
                        new Typeface("Arial"),
                        10,
                        Brushes.Black);

                    drawingContext.DrawText(directorText, new Point(25, 95));
                }

                // Преобразуем DrawingVisual в изображение
                RenderTargetBitmap rtb = new RenderTargetBitmap(150, 150, 96, 96, PixelFormats.Pbgra32);
                rtb.Render(drawingVisual);

                stampImage.Source = rtb;

                // Добавляем изображение в параграф
                stampAndSignatures.Inlines.Add(new InlineUIContainer(stampImage));

                document.Blocks.Add(stampAndSignatures);

                // Подпись клиента
                Paragraph signature = new Paragraph(new Run("Подпись клиента: ____________________"));
                signature.Margin = new Thickness(0, 15, 0, 0);
                document.Blocks.Add(signature);

                // Благодарность
                Paragraph thankYou = new Paragraph(new Run("Спасибо за покупку! Желаем приятных тренировок!"));
                thankYou.TextAlignment = TextAlignment.Center;
                thankYou.Margin = new Thickness(0, 15, 0, 0);
                document.Blocks.Add(thankYou);

                return document;
            }
        }

        private static Paragraph CreateInfoLine(string label, string value)
        {
            Paragraph paragraph = new Paragraph();
            Run labelRun = new Run(label);
            labelRun.FontWeight = FontWeights.SemiBold;
            paragraph.Inlines.Add(labelRun);
            paragraph.Inlines.Add(new Run(" " + value));
            paragraph.Margin = new Thickness(0, 3, 0, 3);
            return paragraph;
        }

        private static Paragraph CreateServiceLine(string name, decimal price)
        {
            Paragraph paragraph = new Paragraph();
            paragraph.Inlines.Add(new Run(name));
            paragraph.Inlines.Add(new Run($" - {price:N2} ₽"));
            paragraph.Margin = new Thickness(15, 3, 0, 3);
            return paragraph;
        }

        private static Paragraph CreateSeparator()
        {
            Paragraph paragraph = new Paragraph(new Run("------------------------------------------------"));
            paragraph.TextAlignment = TextAlignment.Center;
            paragraph.Margin = new Thickness(0, 5, 0, 5);
            return paragraph;
        }

        private static decimal CalculateBonus(Sales sale, decimal usedBonusAmount)
        {
            // Базовые бонусные баллы - 5% от стоимости, оплаченной не бонусами
            decimal basePrice = sale.PriceSold ?? 0;
            decimal bonusBase = basePrice - usedBonusAmount;

            if (bonusBase <= 0)
                return 0;

            decimal bonus = bonusBase * 0.05m;

            // Дополнительные баллы за количество занятий (если есть)
            if (sale.RemainingVisits.HasValue)
            {
                int visits = sale.RemainingVisits.Value;
                decimal visitMultiplier;

                if (visits == 8)
                    visitMultiplier = 1.2m;   // +20% для 8 занятий
                else if (visits == 12)
                    visitMultiplier = 1.5m;   // +50% для 12 занятий
                else
                    visitMultiplier = 1.0m;   // Без бонуса для других значений

                bonus *= visitMultiplier;
            }

            return Math.Round(bonus, 2);
        }
    }
}