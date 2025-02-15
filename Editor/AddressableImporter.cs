﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using System;
using System.IO;
using System.Text.RegularExpressions;

public class AddressableImporter : AssetPostprocessor
{
    static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
    {
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        var importSettings = AddressableImportSettings.Instance;
        if (importSettings == null || importSettings.rules == null || importSettings.rules.Count == 0)
            return;
        var entriesAdded = new List<AddressableAssetEntry>();
        foreach (string path in importedAssets)
        {
            foreach (var rule in importSettings.rules)
            {
                if (rule.Match(path))
                {
                    // The regex to apply to the path. If Simplified is ticked, it a pattern that matches any path, capturing the path, filename and extension.
                    // If the mode is Wildcard, the pattern will match and capture the entire path string.
                    string pathRegex =
                        rule.simplified
                        ? @"(?<path>.*[/\\])+(?<filename>.+?)(?<extension>\.[^.]*$|$)"
                        :  (rule.matchType == AddressableImportRuleMatchType.Wildcard
                            ? @"(.*)"
                            : rule.path);

                    // The replacement string passed into Regex.Replace. If Simplified is ticked, it's the filename, without the extension.
                    // If the mode is Wildcard, it's the entire path, i.e. the first capture group.
                    string addressReplacement =
                        rule.simplified
                        ? @"${filename}"
                        :  (rule.matchType == AddressableImportRuleMatchType.Wildcard
                            ? @"$1"
                            : rule.addressReplacement);

                    var entry = CreateOrUpdateAddressableAssetEntry(settings, path, rule.groupName, rule.labels, pathRegex, addressReplacement);

                    if (entry != null)
                    {
                        entriesAdded.Add(entry);
                        if (rule.HasLabel)
                            Debug.LogFormat("[AddressableImporter] Entry created for {0} with address {1} and labels {2}", path, entry.address, string.Join(", ", entry.labels));
                        else
                            Debug.LogFormat("[AddressableImporter] Entry created for {0} with address {1}", path, entry.address);
                    }
                }
            }
        }
        if (entriesAdded.Count > 0)
        {
            settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryMoved, entriesAdded, true);
            AssetDatabase.SaveAssets();
        }
    }

    static AddressableAssetEntry CreateOrUpdateAddressableAssetEntry(AddressableAssetSettings settings, string path, string groupName, IEnumerable<string> labels, string pathRegex, string addressReplacement)
    {
        var group = GetGroup(settings, groupName);
        if (group == null)
        {
            Debug.LogErrorFormat("[AddressableImporter] Failed to find group {0} when importing {1}. Please check the group exists, then reimport the asset.", groupName, path);
            return null;
        }
        var guid = AssetDatabase.AssetPathToGUID(path);
        var entry = settings.CreateOrMoveEntry(guid, group);
        // Override address if address is a path
        if (string.IsNullOrEmpty(entry.address) || entry.address.StartsWith("Assets/"))
        {
            if (!string.IsNullOrEmpty(pathRegex) && !string.IsNullOrEmpty(addressReplacement))
                entry.address = Regex.Replace(path, pathRegex, addressReplacement);
            else
                entry.address = path;
        }

        // Add labels
        foreach (var label in labels)
        {
            entry.labels.Add(label);
        }
        return entry;
    }

    /// <summary>
    /// Find asset group by given name. Return default group if given name is null.
    /// </summary>
    static AddressableAssetGroup GetGroup(AddressableAssetSettings settings, string groupName)
    {
        if (groupName != null)
            groupName.Trim();
        if (string.IsNullOrEmpty(groupName))
            return settings.DefaultGroup;
        return settings.groups.Find(g => g.Name == groupName);
    }

}

