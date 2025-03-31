using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data.Entity;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace FitnessCenterIS.View.Windows
{
    public partial class AddSeasonticketWindow : Window
    {
        private readonly BDFitnessClubDipEntities _dbContext;
        private Seasontickets _seasonticket;
        private ObservableCollection<SeasonticketServices> _seasonticketServices;

        public AddSeasonticketWindow(BDFitnessClubDipEntities dbContext, Seasontickets seasonticket = null)
        {
            InitializeComponent();
            _dbContext = dbContext;
            _seasonticketServices = new ObservableCollection<SeasonticketServices>();

            if (seasonticket != null)
            {
                _seasonticket = seasonticket;
                Title = "Редактирование абонемента";
                LoadSeasonticketServices();
            }
            else
            {
                _seasonticket = new Seasontickets
                {
                    CreatedDateTime = DateTime.Now,
                    StatusSeasonticket = "Active"
                };
                Title = "Новый абонемент";
            }

            DataContext = _seasonticket;
            SeasonticketServicesDataGrid.ItemsSource = _seasonticketServices;
            LoadAvailableServices();

            LoadComboBoxes();
        }

        private void LoadComboBoxes()
        {
            // Очищаем существующие элементы в StatusComboBox
            StatusComboBox.Items.Clear();

            // Добавляем русские значения
            StatusComboBox.Items.Add("Активен");
            StatusComboBox.Items.Add("Не активен");
        }


        private void LoadAvailableServices()
        {
            var services = _dbContext.Services.ToList();
            ServicesDataGrid.ItemsSource = services;
        }

        private void LoadSeasonticketServices()
        {
            var services = _dbContext.SeasonticketServices
                .Where(ss => ss.SeasonticketID == _seasonticket.SeasonticketID)
                .ToList();
            foreach (var service in services)
            {
                _seasonticketServices.Add(service);
            }
        }

        private void AddServiceToSeasonticket_Click(object sender, RoutedEventArgs e)
        {
            var selectedService = ServicesDataGrid.SelectedItem as Services;
            if (selectedService != null)
            {
                if (int.TryParse(VisitsCountTextBox.Text, out int visitsCount) && visitsCount > 0)
                {
                    var seasonticketService = new SeasonticketServices
                    {
                        ServiceID = selectedService.ServiceID,
                        VisitLimit = visitsCount,
                        Services = selectedService
                    };
                    _seasonticketServices.Add(seasonticketService);
                    RecalculateTotalPrice();
                }
                else
                {
                    MessageBox.Show("Введите корректное количество посещений");
                }
            }
            else
            {
                MessageBox.Show("Выберите услугу для добавления");
            }
        }

        private void RemoveServiceFromSeasonticket_Click(object sender, RoutedEventArgs e)
        {
            var selectedService = SeasonticketServicesDataGrid.SelectedItem as SeasonticketServices;
            if (selectedService != null)
            {
                _seasonticketServices.Remove(selectedService);
                RecalculateTotalPrice();
            }
            else
            {
                MessageBox.Show("Выберите услугу для удаления");
            }
        }

        private void RecalculateTotalPrice()
        {
            decimal totalPrice = _seasonticketServices.Sum(ss => ss.Services.Price * ss.VisitLimit ?? 0);
            _seasonticket.Price = totalPrice;
            PriceTextBox.Text = totalPrice.ToString("C");
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_seasonticket.Name))
            {
                MessageBox.Show("Введите название абонемента");
                return;
            }

            if (_seasonticket.ValidityDuration <= 0)
            {
                MessageBox.Show("Срок действия должен быть больше нуля");
                return;
            }

            if (_seasonticketServices.Count == 0)
            {
                MessageBox.Show("Добавьте хотя бы одну услугу в абонемент");
                return;
            }

            if (_seasonticket.SeasonticketID == 0)
            {
                _dbContext.Seasontickets.Add(_seasonticket);
            }
            else
            {
                _dbContext.Entry(_seasonticket).State = EntityState.Modified;
            }

            foreach (var service in _seasonticketServices)
            {
                if (service.SeasonticketServiceID == 0)
                {
                    service.SeasonticketID = _seasonticket.SeasonticketID;
                    _dbContext.SeasonticketServices.Add(service);
                }
                else
                {
                    _dbContext.Entry(service).State = EntityState.Modified;
                }
            }

            _dbContext.SaveChanges();
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
