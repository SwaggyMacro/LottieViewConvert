using System;
using System.Collections.Generic;
using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Controls.Templates;

namespace LottieViewConvert.Common;

public class ViewLocator : IDataTemplate
{
    private readonly Dictionary<object, Control> _controlCache = new();

    public Control Build(object? data)
    {
        if (data is null)
            return new TextBlock { Text = "Data is null." };

        var fullName = data.GetType().FullName;

        if (string.IsNullOrWhiteSpace(fullName))
            return new TextBlock { Text = "Type has no name, or name is empty." };

        // Check if the data is a model type that isn't meant to have a view
        if (fullName.Contains(".Models."))
        {
            // For model objects, return a basic TextBlock that just shows the string representation
            // This prevents trying to locate views for model objects
            return new TextBlock { Text = data.ToString() ?? fullName };
        }

        var name = fullName.Replace("ViewModel", "View");
        var type = Type.GetType(name);

        if (type is null)
            return new TextBlock { Text = $"No View For {name}." };

        if (!_controlCache.TryGetValue(data, out var res))
        {
            res = (Control)Activator.CreateInstance(type)!;
            _controlCache[data] = res;
        }

        res.DataContext = data;
        return res;
    }

    public bool Match(object? data)
    {
        // Only match objects that should have views
        // Exclude model objects that are used as data items
        if (data == null) return false;

        var fullName = data.GetType().FullName;
        if (string.IsNullOrEmpty(fullName)) return false;

        // Don't try to create views for model objects
        if (fullName.Contains(".Models."))
            return false;

        return data is INotifyPropertyChanged;
    }
}