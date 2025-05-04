using FitnessCenterIS.Model;
using FitnessCenterIS.View.Windows;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace FitnessCenterIS.View.Pages
{
    public partial class GroupsPage : Page
    {
        private readonly BDFitnessClubDipEntities _dbContext;
        private List<GroupViewModel> _groups;

        public GroupsPage()
        {
            InitializeComponent();
            _dbContext = new BDFitnessClubDipEntities();
            LoadGroups();
        }

        private void LoadGroups()
        {
            try
            {
                _groups = _dbContext.Groups
                    .Select(g => new GroupViewModel
                    {
                        GroupID = g.GroupID,
                        Name = g.Name,
                        Description = g.Description,
                        ServiceName = g.Services.Name,
                        LimitCapacity = g.LimitCapacity,
                        Discount = g.Discount,
                        StatusActivity = g.StatusActivity
                    }).ToList();

                GroupsDataGrid.ItemsSource = _groups;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки групп: {ex.Message}", "Ошибка", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void AddGroupButton_Click(object sender, RoutedEventArgs e)
        {
            var addWindow = new AddEditGroupWindow(_dbContext);
            if (addWindow.ShowDialog() == true)
            {
                LoadGroups();
            }
        }

        private void EditGroupButton_Click(object sender, RoutedEventArgs e)
        {
            if (GroupsDataGrid.SelectedItem is GroupViewModel groupViewModel)
            {
                var group = _dbContext.Groups.Find(groupViewModel.GroupID);
                if (group != null)
                {
                    var editWindow = new AddEditGroupWindow(_dbContext, group);
                    if (editWindow.ShowDialog() == true)
                    {
                        LoadGroups();
                    }
                }
            }
        }

        private void DeleteGroupButton_Click(object sender, RoutedEventArgs e)
        {
            if (GroupsDataGrid.SelectedItem is GroupViewModel groupViewModel)
            {
                var result = MessageBox.Show($"Вы уверены, что хотите удалить группу '{groupViewModel.Name}'?", 
                    "Подтверждение удаления", MessageBoxButton.YesNo, MessageBoxImage.Question);
                
                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        var group = _dbContext.Groups.Find(groupViewModel.GroupID);
                        if (group != null)
                        {
                            _dbContext.Groups.Remove(group);
                            _dbContext.SaveChanges();
                            LoadGroups();
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Ошибка удаления группы: {ex.Message}", "Ошибка", 
                            MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        private void GroupsDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Обработчик для выбора группы
        }

        private void GroupsDataGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            EditGroupButton_Click(sender, e);
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string searchText = SearchTextBox.Text.ToLower();
            
            if (string.IsNullOrWhiteSpace(searchText))
            {
                GroupsDataGrid.ItemsSource = _groups;
            }
            else
            {
                var filteredGroups = _groups.Where(g => 
                    g.Name.ToLower().Contains(searchText) ||
                    g.Description?.ToLower().Contains(searchText) == true ||
                    g.ServiceName?.ToLower().Contains(searchText) == true ||
                    g.StatusActivity?.ToLower().Contains(searchText) == true
                ).ToList();

                GroupsDataGrid.ItemsSource = filteredGroups;
            }
        }

        private void ExportGroupsButton_Click(object sender, RoutedEventArgs e)
        {
            // Реализация экспорта групп
            MessageBox.Show("Функция экспорта групп будет реализована", "Информация", 
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    public class GroupViewModel
    {
        public int GroupID { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string ServiceName { get; set; }
        public int? LimitCapacity { get; set; }
        public decimal? Discount { get; set; }
        public string StatusActivity { get; set; }
    }
}