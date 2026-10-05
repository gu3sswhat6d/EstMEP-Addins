using System;
using System.Windows.Forms;
using Autodesk.Fabrication.ApplicationServices;
using EstMepAddins.DimensionChecker.UI;

namespace EstMepAddins.DimensionChecker.AddIn
{
    /// <summary>
    /// ESTmep add-in entry point. ESTmep and CAMduct call Execute() when the add-in is loaded
    /// from the Add-Ins menu; we use it to open the checker window.
    /// </summary>
    public class DimensionCheckerApp : IExternalApplication
    {
        public void Execute()
        {
            try
            {
                using (var form = new DimensionCheckerForm(new FabricationItemDrawingService()))
                {
                    form.ShowDialog();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "Submittal Dimension Check", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
