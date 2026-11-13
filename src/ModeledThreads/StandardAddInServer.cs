using System.Runtime.InteropServices;
using Inventor;
using ModeledThreads.Core;
using ModeledThreads.UI;
using ModeledThreads.Utilities;

namespace ModeledThreads;

/// <summary>
/// The add-in's entry point: it puts a "Create Thread" button on the ribbon and
/// runs the thread dialog.
/// </summary>
[ComVisible(true)]
[Guid("b401cf90-402b-4d9e-8d04-9152e75e5017")]
[ProgId("ModeledThreads.StandardAddInServer")]
public sealed class StandardAddInServer : ApplicationAddInServer
{
    private const string ButtonInternalName = "ModeledThreads.Create";
    private const string EditInternalName = "ModeledThreads.Edit";
    private const string TabInternalName = "ModeledThreads.Tab";
    private const string PanelInternalName = "ModeledThreads.Panel";

    private Inventor.Application? _app;
    private ButtonDefinition? _button;
    private ButtonDefinition? _editButton;
    private RibbonTab? _tab;
    private bool _editing;

    public object Automation => null!;

    public void Activate(ApplicationAddInSite addInSiteObject, bool firstTime)
    {
        try
        {
            _app = addInSiteObject.Application;
            Log.Info($"Modeled Threads activated (first time: {firstTime}).");

            if (firstTime)
            {
                BuildRibbon();

                // Double-clicking a thread in the model opens the same dialog,
                // as editing any other feature would.
                _app.CommandManager.UserInputEvents.OnDoubleClick += UserInputEvents_OnDoubleClick;
            }
        }
        catch (Exception ex)
        {
            Log.Error("Could not activate the add-in.", ex);
        }
    }

    public void Deactivate()
    {
        try
        {
            if (_app is not null)
            {
                try
                {
                    _app.CommandManager.UserInputEvents.OnDoubleClick -= UserInputEvents_OnDoubleClick;
                }
                catch
                {
                    // Inventor is closing; there is nothing to unhook.
                }
            }

            _button = null;
            _editButton = null;
            _tab = null;
            _app = null;
        }
        catch (Exception ex)
        {
            Log.Error("Could not deactivate the add-in.", ex);
        }
    }

    /// <summary>Inventor calls this when the user runs the add-in's command.</summary>
    public void ExecuteCommand(int commandId)
    {
        OnCreateThread();
    }

    public void Terminate(ApplicationAddInSite addInSiteObject)
    {
        try
        {
            _button = null;
            _editButton = null;
            _tab = null;
            _app = null;
        }
        catch (Exception ex)
        {
            Log.Error("Could not terminate the add-in.", ex);
        }
    }

    private void BuildRibbon()
    {
        if (_app is null)
        {
            return;
        }

        try
        {
            ControlDefinitions definitions = _app.CommandManager.ControlDefinitions;

            ButtonDefinition? definition = FindDefinition(definitions, ButtonInternalName);
            if (definition is null)
            {
                // An icon is optional: if the picture cannot be made, the button
                // simply has none.
                object? icon = ButtonIcons.ThreadIcon();
                Log.Info(icon is null ? "No icon for the button." : "The button's icon was made.");

                definition = definitions.AddButtonDefinition(
                    "Create Thread",
                    ButtonInternalName,
                    CommandTypesEnum.kEditMaskCmdType,
                    Globals.ClientId,
                    "Cuts a real thread into a cylindrical face.",
                    "Cuts a modelled thread into the selected cylindrical face, using the "
                    + "standard's own profile rather than Inventor's cosmetic thread.",
                    icon,
                    icon,
                    ButtonDisplayEnum.kDisplayTextInLearningMode);
            }

            ButtonDefinition? editDefinition = FindDefinition(definitions, EditInternalName);
            if (editDefinition is null)
            {
                editDefinition = definitions.AddButtonDefinition(
                    "Edit Thread",
                    EditInternalName,
                    CommandTypesEnum.kEditMaskCmdType,
                    Globals.ClientId,
                    "Changes a thread that is already in the model.",
                    "Opens the thread dialog on a modelled thread, so its size, "
                    + "length, clearance and end treatments can be changed.",
                    ButtonIcons.ThreadIcon(),
                    ButtonIcons.ThreadIcon(),
                    ButtonDisplayEnum.kDisplayTextInLearningMode);
            }

            ButtonDefinition button = definition;
            ButtonDefinition editButton = editDefinition;

            // The handler is attached to the definition object itself, so the
            // add-in has to keep hold of it: let it be collected and the
            // connection goes with it, leaving a button that does nothing.
            if (_button is null)
            {
                _button = button;
                button.OnExecute += (context) => OnCreateThread();
            }

            if (_editButton is null)
            {
                _editButton = editButton;
                editButton.OnExecute += (context) => OnEditThread(null);
            }

            // Inventor keeps one ribbon per environment: one with no document
            // open, one for a part, one for an assembly and so on. The tab has to
            // go on every one of them, or it vanishes the moment a document opens.
            int placed = 0;
            foreach (Ribbon ribbon in _app.UserInterfaceManager.Ribbons)
            {
                if (EnsureTab(ribbon, button, editButton))
                {
                    placed++;
                }
            }

            Log.Info($"The Modeled Threads buttons are on {placed} ribbon(s).");
        }
        catch (Exception ex)
        {
            Log.Error("Could not build the ribbon.", ex);
        }
    }

    /// <summary>Puts the tab, its panel and the buttons on one ribbon.</summary>
    private bool EnsureTab(Ribbon ribbon, ButtonDefinition button, ButtonDefinition editButton)
    {
        try
        {
            RibbonTabs tabs = ribbon.RibbonTabs;

            RibbonTab? tab = FindTab(tabs, TabInternalName);
            if (tab is null)
            {
                tab = tabs.Add("Modeled Threads", TabInternalName, Globals.ClientId);
            }

            RibbonPanels panels = tab.RibbonPanels;

            RibbonPanel? panel = FindPanel(panels, PanelInternalName);
            if (panel is null)
            {
                panel = panels.Add("Threads", PanelInternalName, Globals.ClientId);
            }

            // Only one copy of each button on the panel.
            bool hasCreate = false;
            bool hasEdit = false;
            foreach (CommandControl control in panel.CommandControls)
            {
                if (control.ControlDefinition is ButtonDefinition existing)
                {
                    hasCreate |= existing.InternalName == ButtonInternalName;
                    hasEdit |= existing.InternalName == EditInternalName;
                }
            }

            if (!hasCreate)
            {
                panel.CommandControls.AddButton(button, true, true);
            }

            if (!hasEdit)
            {
                panel.CommandControls.AddButton(editButton, true, true);
            }

            return true;
        }
        catch (Exception ex)
        {
            Log.Warn($"Could not put the tab on the ribbon '{ribbon.InternalName}': {ex.Message}");
            return false;
        }
    }

    private static RibbonTab? FindTab(RibbonTabs tabs, string internalName)
    {
        foreach (RibbonTab tab in tabs)
        {
            if (tab.InternalName == internalName)
            {
                return tab;
            }
        }

        return null;
    }

    private static RibbonPanel? FindPanel(RibbonPanels panels, string internalName)
    {
        foreach (RibbonPanel panel in panels)
        {
            if (panel.InternalName == internalName)
            {
                return panel;
            }
        }

        return null;
    }

    private static ButtonDefinition? FindDefinition(ControlDefinitions definitions, string internalName)
    {
        foreach (ControlDefinition definition in definitions)
        {
            if (definition.InternalName == internalName && definition is ButtonDefinition button)
            {
                return button;
            }
        }

        return null;
    }

    /// <summary>
    /// Picks a cylindrical face and then opens the dialog for it.
    /// </summary>
    private void OnCreateThread()
    {
        Inventor.Application? app = _app;
        if (app is null)
        {
            return;
        }

        Log.Info("Create Thread: the command was started.");

        try
        {
            Document? document = app.ActiveDocument;
            if (document is not PartDocument part)
            {
                MessageBox.Show(
                    "Open a part first: a thread is cut into a cylindrical face of one.",
                    "Modeled Threads",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            while (true)
            {
                // A face the user has already picked is used as it is; otherwise
                // one is asked for.
                Face? face = SelectedCylindricalFace(part) ?? PickCylindricalFace(app);
                if (face is null)
                {
                    return;
                }

                FaceProbe? probe = FaceProbeReader.Probe(part, face, app.TransientGeometry);
                if (probe is null)
                {
                    MessageBox.Show(
                        "That face could not be read. Pick a plain cylindrical face.",
                        "Modeled Threads",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    continue;
                }

                using var form = new CreateThreadForm(app, part, probe);
                DialogResult result = form.ShowDialog();

                if (result == DialogResult.Retry)
                {
                    // Another face was asked for.
                    continue;
                }

                return;
            }
        }
        catch (Exception ex)
        {
            Log.Error("The thread command failed.", ex);
            MessageBox.Show(
                $"The thread could not be made: {ex.Message}",
                "Modeled Threads",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    /// <summary>
    /// Opens the thread dialog on a thread that is already in the model: the one
    /// selected, or one the user is asked for.
    /// </summary>
    private void OnEditThread(ThreadRecord? thread)
    {
        Inventor.Application? app = _app;
        if (app is null || _editing)
        {
            return;
        }

        _editing = true;

        try
        {
            if (app.ActiveDocument is not PartDocument part)
            {
                MessageBox.Show(
                    "Open the part that has the thread in it.",
                    "Modeled Threads",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            ThreadRecord? record = thread ?? ThreadInSelection(part);

            if (record is null)
            {
                object? picked = app.CommandManager.Pick(
                    SelectionFilterEnum.kPartFeatureFilter,
                    "Select a modelled thread. Press Escape to stop.");

                if (!ThreadMetadata.TryReadFrom(picked, out record, out _) || record is null)
                {
                    Log.Info("Edit Thread: nothing was picked.");
                    return;
                }
            }

            // The dialog opens on what the thread was made with; the surface it
            // was cut into is checked again when the new numbers are applied.
            FaceProbe probe = FaceProbe.FromRecord(record, app.TransientGeometry);

            Log.Info($"Editing the thread '{record.Designation}'.");
            using var form = new CreateThreadForm(app, part, probe, record);
            form.ShowDialog();
        }
        catch (Exception ex)
        {
            Log.Error("The edit command failed.", ex);
            MessageBox.Show(
                $"The thread could not be opened for editing: {ex.Message}",
                "Modeled Threads",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        finally
        {
            _editing = false;
        }
    }

    /// <summary>The thread that something in the selection belongs to, if any.</summary>
    private static ThreadRecord? ThreadInSelection(PartDocument part)
    {
        try
        {
            SelectSet selection = part.SelectSet;
            for (int i = 1; i <= selection.Count; i++)
            {
                if (ThreadMetadata.TryReadFrom(selection[i], out ThreadRecord? record, out _))
                {
                    Log.Info("Using the thread that was already selected.");
                    return record;
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"Could not read the selection: {ex.Message}");
        }

        return null;
    }

    /// <summary>
    /// A double-click on a thread — in the browser or on its geometry — opens the
    /// thread dialog instead of Inventor's own editor for the coil.
    /// </summary>
    private void UserInputEvents_OnDoubleClick(
        ObjectsEnumerator selectedEntities,
        SelectionDeviceEnum selectionDevice,
        MouseButtonEnum button,
        ShiftStateEnum shiftKeys,
        Inventor.Point modelPosition,
        Point2d viewPosition,
        Inventor.View view,
        NameValueMap additionalInfo,
        out HandlingCodeEnum handlingCode)
    {
        handlingCode = HandlingCodeEnum.kEventNotHandled;

        try
        {
            int count = 0;
            try
            {
                count = selectedEntities?.Count ?? 0;
            }
            catch (Exception ex)
            {
                Log.Warn($"The double-click's entities could not be read: {ex.Message}");
                return;
            }

            for (int i = 1; i <= count; i++)
            {
                if (!ThreadMetadata.TryReadFrom(selectedEntities[i], out ThreadRecord? record, out object? feature))
                {
                    continue;
                }

                Log.Info($"Double-click on the thread '{record!.Designation}' ({ThreadMetadata.NameOf(feature)}).");
                handlingCode = HandlingCodeEnum.kEventHandled;

                // The dialog is opened once Inventor has finished with the
                // double-click, so the click is not caught by the dialog's own
                // message loop.
                ThreadRecord opening = record;

                // A one-shot timer lets Inventor finish dispatching the click
                // before a modal dialog is opened from inside its event: the
                // dialog's own loop then starts on a quiet thread.
                var timer = new System.Windows.Forms.Timer { Interval = 10 };
                timer.Tick += (_, _) =>
                {
                    timer.Stop();
                    timer.Dispose();
                    OnEditThread(opening);
                };
                timer.Start();

                return;
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"The double-click could not be handled: {ex.Message}");
        }
    }

    /// <summary>The first cylindrical face in the document's selection, if any.</summary>
    private static Face? SelectedCylindricalFace(PartDocument part)
    {
        try
        {
            SelectSet selection = part.SelectSet;
            for (int i = 1; i <= selection.Count; i++)
            {
                if (selection[i] is Face face && face.SurfaceType == SurfaceTypeEnum.kCylinderSurface)
                {
                    Log.Info("Using the cylindrical face that was already selected.");
                    return face;
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"Could not read the selection: {ex.Message}");
        }

        return null;
    }

    /// <summary>Lets the user pick a cylindrical face of the part.</summary>
    private static Face? PickCylindricalFace(Inventor.Application app)
    {
        try
        {
            object? picked = app.CommandManager.Pick(
                SelectionFilterEnum.kPartFaceCylindricalFilter,
                "Select the cylindrical face to thread. Press Escape to stop.");

            return picked switch
            {
                Face face => face,
                ObjectCollection collection => collection.Count > 0 ? collection[1] as Face : null,
                _ => null
            };
        }
        catch (Exception ex)
        {
            Log.Info($"The face pick was cancelled: {ex.Message}");
            return null;
        }
    }
}
