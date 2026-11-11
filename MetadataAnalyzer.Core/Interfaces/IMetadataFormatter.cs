using MetadataAnalyzer.Core.Models;

namespace MetadataAnalyzer.Core.Interfaces;

public interface IMetadataFormatter
{
    string FormatTree(AssemblyModel assembly);

    string FormatDetails(AssemblyModel assembly);

    string FormatMemberDetails(ClassModel type, string memberName);
}
