using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Package;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.OLE.Interop;

namespace Gauge.VisualStudio.LangaugeService
{
    class GaugeLanguageService : LanguageService
    {
        private LanguagePreferences m_preferences;

        public override string GetFormatFilterList()
        {
            throw new NotImplementedException();
        }

        public override LanguagePreferences GetLanguagePreferences()
        {
            if (m_preferences == null)
            {
                m_preferences = new LanguagePreferences(this.Site, typeof(GaugeLanguageService).GUID, this.Name);
                m_preferences.Init();
            }
            return m_preferences;
        }

        public override IScanner GetScanner(IVsTextLines buffer)
        {
            return null;
        }

        public override string Name
        {
            get { return "Gauge"; }
        }

        public override AuthoringScope ParseSource(ParseRequest req)
        {
            throw new NotImplementedException();
        }
    }
}
