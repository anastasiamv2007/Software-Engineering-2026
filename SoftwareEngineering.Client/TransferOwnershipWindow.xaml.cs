using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using SoftwareEngineering.BLL.Interfaces;

namespace SoftwareEngineering.Client;

public partial class TransferOwnershipWindow : Window
{
    private readonly IProjectMemberService _projectMemberService;
    private readonly Guid _projectId;
    private readonly Guid _currentUserId;

    public bool IsTransferred { get; private set; }

    public TransferOwnershipWindow(
        IProjectMemberService projectMemberService,
        Guid projectId,
        string projectName,
        Guid currentUserId,
        IReadOnlyList<ProjectMemberOption> candidates)
    {
        InitializeComponent();

        _projectMemberService = projectMemberService;
        _projectId = projectId;
        _currentUserId = currentUserId;

        TxtProjectSubtitle.Text = $"Проєкт: «{projectName}»";

        foreach (var candidate in candidates)
        {
            CmbNewOwner.Items.Add(new ComboBoxItem
            {
                Content = $"{candidate.FullName} ({candidate.Email})",
                Tag = candidate
            });
        }

        if (CmbNewOwner.Items.Count > 0)
        {
            CmbNewOwner.SelectedIndex = 0;
        }
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private async void BtnConfirm_Click(object sender, RoutedEventArgs e)
    {
        if (CmbNewOwner.SelectedItem is not ComboBoxItem { Tag: ProjectMemberOption selectedMember })
        {
            MessageBox.Show("Оберіть учасника зі списку.", "Увага", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var confirm = MessageBox.Show(
            $"Ви дійсно впевнені, що хочете передати права власності користувачу {selectedMember.FullName} ({selectedMember.Email})?\n\nЦю дію неможливо скасувати самостійно — ви станете співвиконавцем.",
            "Підтвердження передачі власності",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirm != MessageBoxResult.Yes)
        {
            return;
        }

        BtnConfirm.IsEnabled = false;
        try
        {
            var result = await _projectMemberService.TransferOwnershipAsync(_projectId, selectedMember.UserId, _currentUserId);
            if (result.IsFailure)
            {
                MessageBox.Show(result.Error?.Message ?? "Помилка передачі прав.", "Помилка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            IsTransferred = true;
            MessageBox.Show(
                $"Права власності успішно передано користувачу {selectedMember.FullName}. Тепер ваша роль у проєкті — співвиконавець.",
                "Власність передано",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            DialogResult = true;
            Close();
        }
        finally
        {
            BtnConfirm.IsEnabled = true;
        }
    }
}