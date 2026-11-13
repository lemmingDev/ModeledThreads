using System.Globalization;
using ModeledThreads.Core;
using ModeledThreads.Utilities;

namespace ModeledThreads.UI;

/// <summary>
/// The dialog that makes a thread: the size, the options, and the face it goes
/// on. The thread's profile comes from the standard, not from Inventor.
/// </summary>
public sealed class CreateThreadForm : Form
{
    private readonly Inventor.Application _app;
    private readonly Inventor.PartDocument _document;
    private readonly FaceProbe _probe;

    private readonly Label _faceLabel = new();
    private readonly Label _sizeLabel = new();
    private readonly Label _warningLabel = new();
    private readonly ComboBox _formCombo = new();
    private readonly ComboBox _designationCombo = new();
    private readonly TextBox _lengthText = new();
    private readonly TextBox _clearanceText = new();
    private readonly NumericUpDown _starts = new();
    private readonly RadioButton _rightHand = new();
    private readonly RadioButton _leftHand = new();
    private readonly CheckBox _chamferStart = new();
    private readonly CheckBox _chamferEnd = new();
    private readonly CheckBox _reverse = new();
    private readonly Button _createButton = new();
    private readonly Button _closeButton = new();
    private readonly Button _otherFaceButton = new();

    public CreateThreadForm(Inventor.Application app, Inventor.PartDocument document, FaceProbe probe)
    {
        _app = app;
        _document = document;
        _probe = probe;

        BuildUi();
        LoadForms();
        SelectSuggestedSize();
    }

    private void BuildUi()
    {
        Text = "Create a Modelled Thread";
        Name = "CreateThreadForm";
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowIcon = false;
        ClientSize = new Size(640, 520);
        Font = new Font("Segoe UI", 9F);

        var outer = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 12,
            Padding = new Padding(14, 12, 14, 6)
        };
        // The captions take the width their own text needs, which changes with
        // the system's font and scaling; the fields take what is left.
        outer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        outer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        for (int i = 0; i < 12; i++)
        {
            outer.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        }

        _faceLabel.Text = DescribeFace();
        _faceLabel.AutoSize = true;
        _faceLabel.MaximumSize = new Size(600, 0);
        _faceLabel.Margin = new Padding(3, 4, 3, 12);
        outer.Controls.Add(_faceLabel, 0, 0);
        outer.SetColumnSpan(_faceLabel, 2);

        AddRow(outer, 1, "Standard", _formCombo);
        _formCombo.DropDownStyle = ComboBoxStyle.DropDownList;
        _formCombo.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        _formCombo.SelectedIndexChanged += (_, _) => LoadDesignations();

        AddRow(outer, 2, "Designation", _designationCombo);
        _designationCombo.DropDownStyle = ComboBoxStyle.DropDown;
        _designationCombo.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        _designationCombo.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
        _designationCombo.AutoCompleteSource = AutoCompleteSource.ListItems;
        _designationCombo.SelectedIndexChanged += (_, _) => UpdateSizeLabel();
        _designationCombo.TextChanged += (_, _) => UpdateSizeLabel();

        AddRow(outer, 3, "Length (mm)", _lengthText);
        _lengthText.Width = 120;
        _lengthText.Text = string.Empty;
        _lengthText.TextChanged += (_, _) => UpdateSizeLabel();
        _tip.SetToolTip(_lengthText, "Leave it empty for the whole face.");

        AddRow(outer, 4, "Clearance (mm)", _clearanceText);
        _clearanceText.Width = 120;
        _clearanceText.Text = "0";
        _tip.SetToolTip(_clearanceText, "Extra material cut from the thread's root: a thinner shaft, a bigger hole.");

        AddRow(outer, 5, "Starts", _starts);
        _starts.Minimum = 1;
        _starts.Maximum = 8;
        _starts.Value = 1;
        _starts.Width = 120;
        _starts.ValueChanged += (_, _) => UpdateSizeLabel();

        var hand = new FlowLayoutPanel { AutoSize = true, Margin = new Padding(0) };
        _rightHand.Text = "Right";
        _rightHand.Checked = true;
        _rightHand.AutoSize = true;
        _leftHand.Text = "Left";
        _leftHand.AutoSize = true;
        hand.Controls.Add(_rightHand);
        hand.Controls.Add(_leftHand);
        AddRow(outer, 6, "Hand", hand);

        _chamferStart.Text = "45° lead-in at the open end";
        _chamferStart.Checked = true;
        _chamferStart.AutoSize = true;
        _tip.SetToolTip(_chamferStart, "Cuts the cone ISO 4753 asks for, so the thread's last teeth run into it.");
        AddRow(outer, 7, "Lead-in", _chamferStart);

        _chamferEnd.Text = "Relief at a shoulder";
        _chamferEnd.Checked = true;
        _chamferEnd.AutoSize = true;
        _tip.SetToolTip(_chamferEnd, "Takes the last turn away with a one pitch relief, so the thread runs out before the shoulder.");
        AddRow(outer, 8, "Shoulder", _chamferEnd);

        _reverse.Text = "Start at the other end";
        _reverse.AutoSize = true;
        AddRow(outer, 9, "Direction", _reverse);

        _sizeLabel.AutoSize = true;
        _sizeLabel.MaximumSize = new Size(600, 0);
        _sizeLabel.Margin = new Padding(3, 12, 3, 4);
        _sizeLabel.ForeColor = Color.DimGray;
        outer.Controls.Add(_sizeLabel, 0, 10);
        outer.SetColumnSpan(_sizeLabel, 2);

        _warningLabel.AutoSize = true;
        _warningLabel.MaximumSize = new Size(600, 0);
        _warningLabel.Margin = new Padding(3, 2, 3, 4);
        _warningLabel.ForeColor = Color.DarkOrange;
        outer.Controls.Add(_warningLabel, 0, 11);
        outer.SetColumnSpan(_warningLabel, 2);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 52,
            Padding = new Padding(8, 9, 10, 8),
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false
        };

        _createButton.Text = "Create Thread";
        _createButton.AutoSize = true;
        _createButton.Padding = new Padding(16, 3, 16, 3);
        _createButton.Click += CreateButtonOnClick;

        _closeButton.Text = "Close";
        _closeButton.AutoSize = true;
        _closeButton.Padding = new Padding(12, 3, 12, 3);
        _closeButton.DialogResult = DialogResult.Cancel;

        _otherFaceButton.Text = "Pick another face";
        _otherFaceButton.AutoSize = true;
        _otherFaceButton.Padding = new Padding(10, 3, 10, 3);
        _otherFaceButton.Click += (_, _) =>
        {
            DialogResult = DialogResult.Retry;
            Close();
        };

        buttons.Controls.Add(_closeButton);
        buttons.Controls.Add(_createButton);
        buttons.Controls.Add(_otherFaceButton);

        Controls.Add(outer);
        Controls.Add(buttons);

        CancelButton = _closeButton;
        AcceptButton = _createButton;

        // As tall as the rows need, since the face's description wraps.
        outer.PerformLayout();
        int needed = outer.GetPreferredSize(new Size(ClientSize.Width, 0)).Height;
        ClientSize = new Size(ClientSize.Width, needed + buttons.Height + 8);
    }

    private readonly ToolTip _tip = new();

    private void AddRow(TableLayoutPanel table, int row, string label, Control control)
    {
        var caption = new Label
        {
            Text = label,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(3, 7, 3, 3)
        };
        control.Margin = new Padding(3, 4, 3, 3);
        table.Controls.Add(caption, 0, row);
        table.Controls.Add(control, 1, row);
    }

    private string DescribeFace() =>
        $"Face: a {( _probe.Internal ? "hole" : "shaft")} of radius {_probe.RadiusMm:0.###} mm, " +
        $"{_probe.LengthMm:0.###} mm long.\n" +
        $"Its lower end is {_probe.LowEnd.ToString().ToLowerInvariant()}, " +
        $"its upper end {_probe.HighEnd.ToString().ToLowerInvariant()}.";

    private void LoadForms()
    {
        _formCombo.Items.Clear();
        foreach (ThreadFormKind form in Enum.GetValues(typeof(ThreadFormKind)).Cast<ThreadFormKind>())
        {
            // A standard with no sizes yet cannot make a thread, so it is not
            // offered.
            if (!ThreadCatalog.Designations(form).Any())
            {
                continue;
            }

            _formCombo.Items.Add(new FormItem(form));
        }

        _formCombo.SelectedItem = _formCombo.Items
            .OfType<FormItem>()
            .FirstOrDefault(i => i.Form == ThreadFormKind.Metric);
    }

    private void LoadDesignations()
    {
        if (_formCombo.SelectedItem is not FormItem item)
        {
            return;
        }

        _designationCombo.Items.Clear();
        foreach (string designation in ThreadCatalog.Designations(item.Form))
        {
            _designationCombo.Items.Add(designation);
        }

        _designationCombo.SelectedIndex = 0;
    }

    /// <summary>
    /// Puts the size that fits the face in the box: a shaft takes its major
    /// diameter, a hole the drill for its thread.
    /// </summary>
    private void SelectSuggestedSize()
    {
        if (_formCombo.SelectedItem is not FormItem item)
        {
            return;
        }

        ThreadSize? fit = ThreadCatalog.Fit(item.Form, _probe.RadiusMm * 2.0, _probe.Internal);
        if (fit is null)
        {
            return;
        }

        string designation = ThreadCatalog.Format(fit.Value);
        int index = _designationCombo.Items.IndexOf(designation);
        if (index >= 0)
        {
            _designationCombo.SelectedIndex = index;
        }
        else
        {
            _designationCombo.Text = designation;
        }
    }

    private bool TryGetSize(out ThreadSize size)
    {
        size = default;

        if (_formCombo.SelectedItem is not FormItem item)
        {
            _warningLabel.Text = "Choose a standard.";
            return false;
        }

        string text = _designationCombo.Text.Trim();
        if (text.Length == 0)
        {
            _warningLabel.Text = "Choose a size.";
            return false;
        }

        if (!ThreadCatalog.TryParse(text, out size, out string error))
        {
            _warningLabel.Text = error;
            return false;
        }

        if (size.Form != item.Form)
        {
            _warningLabel.Text = $"'{text}' is not a {ThreadForms.DisplayName(item.Form)} size.";
            return false;
        }

        return true;
    }

    private void UpdateSizeLabel()
    {
        if (!TryGetSize(out ThreadSize size))
        {
            _sizeLabel.Text = string.Empty;
            return;
        }

        int starts = (int)_starts.Value;

        // The minor diameter is the root of a shaft's groove and the hole a nut's
        // teeth cut to, which are opposite radii of the same profile.
        GrooveProfile groove = ThreadForms.Groove(size.Form, size.MajorMm, size.PitchMm, _probe.Internal);
        double minor = 2.0 * (groove.Internal ? groove.CrestR : groove.RootR);

        // A drill size only means something for a hole: on a shaft it would read
        // as a size to bore the part down to.
        string drill = _probe.Internal
            ? $"   |   drill {ThreadCatalog.TapDrillMm(size):0.###} mm"
            : string.Empty;

        _sizeLabel.Text =
            $"Major {_sizeLabelValue(size.MajorMm)} mm, pitch {_sizeLabelValue(size.PitchMm)} mm" +
            (starts > 1 ? $", lead {_sizeLabelValue(size.PitchMm * starts)} mm" : string.Empty) +
            drill +
            $"   |   minor {_sizeLabelValue(minor)} mm";
    }

    private static string _sizeLabelValue(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);

    private void CreateButtonOnClick(object? sender, EventArgs e)
    {
        _warningLabel.Text = string.Empty;

        if (!TryGetSize(out ThreadSize size))
        {
            return;
        }

        double length = 0;
        if (!string.IsNullOrWhiteSpace(_lengthText.Text)
            && !double.TryParse(_lengthText.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out length))
        {
            _warningLabel.Text = "The length must be a number of millimetres.";
            return;
        }

        double clearance = 0;
        if (!double.TryParse(_clearanceText.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out clearance)
            || clearance < 0 || clearance > 5)
        {
            _warningLabel.Text = "The clearance must be between 0 and 5 millimetres.";
            return;
        }

        int starts = (int)_starts.Value;
        int startSide = ChooseStartSide();

        var options = new ThreadPlanOptions
        {
            Form = size.Form,
            Designation = _designationCombo.Text.Trim(),
            MajorMm = size.MajorMm,
            PitchMm = size.PitchMm,
            LeadMm = size.PitchMm * starts,
            Internal = _probe.Internal,
            ClearanceMm = clearance,
            RightHanded = _rightHand.Checked,
            Starts = starts,
            FaceRadiusMm = _probe.RadiusMm,
            FaceLowMm = _probe.LowCm * 10.0,
            FaceHighMm = _probe.HighCm * 10.0,
            StartSide = startSide,
            LengthMm = length,
            LowEndKind = _probe.LowEnd,
            HighEndKind = _probe.HighEnd,
            ChamferStart = _chamferStart.Checked,
            ChamferEnd = _chamferEnd.Checked
        };

        ThreadPlan plan;
        try
        {
            plan = ThreadPlanBuilder.Build(options);
        }
        catch (ThreadPlanException ex)
        {
            _warningLabel.Text = ex.Message;
            return;
        }

        Cursor = Cursors.WaitCursor;
        _createButton.Enabled = false;

        try
        {
            Inventor.Transaction transaction = _app.TransactionManager.StartTransaction(
                _document.InventorDocument, $"Create thread {plan.Designation}");

            try
            {
                var builder = new ThreadBuilder(_app, _document, _probe);
                builder.Build(plan);

                transaction.End();
            }
            catch (Exception ex)
            {
                try
                {
                    transaction.Abort();
                }
                catch
                {
                    // the message below is the useful one
                }

                Log.Error($"Could not create the thread '{plan.Designation}'.", ex);
                _warningLabel.Text = $"The thread could not be cut: {ex.Message}";
                return;
            }

            Log.Info(
                $"Thread '{plan.Designation}' created: {plan.Turns:0.##} turns, " +
                $"{plan.SweepLengthMm:0.##} mm, clearance {plan.ClearanceMm:0.##} mm.");

            // The model is its own confirmation: only a warning is worth a box
            // to dismiss.
            if (plan.Warning.Length > 0)
            {
                MessageBox.Show(
                    $"The thread was created.\n\n{plan.Warning}",
                    "Modeled Threads",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }

            DialogResult = DialogResult.OK;
            Close();
        }
        finally
        {
            Cursor = Cursors.Default;
            _createButton.Enabled = true;
        }
    }

    /// <summary>
    /// Which end of the face the thread starts at: the free one where there is
    /// only one, unless the user asks for the other.
    /// </summary>
    private int ChooseStartSide()
    {
        bool lowFree = _probe.LowEnd == ThreadEndKind.Free;
        bool highFree = _probe.HighEnd == ThreadEndKind.Free;

        int side;
        if (lowFree && !highFree)
        {
            side = 0;
        }
        else if (highFree && !lowFree)
        {
            side = 1;
        }
        else
        {
            side = 0;
        }

        return _reverse.Checked ? 1 - side : side;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _tip.Dispose();
        }

        base.Dispose(disposing);
    }

    /// <summary>A standard in the list, with its name shown.</summary>
    private sealed class FormItem(ThreadFormKind form)
    {
        public ThreadFormKind Form { get; } = form;

        public override string ToString() =>
            $"{ThreadForms.DisplayName(Form)}  ({ThreadForms.Standard(Form)})";
    }
}
