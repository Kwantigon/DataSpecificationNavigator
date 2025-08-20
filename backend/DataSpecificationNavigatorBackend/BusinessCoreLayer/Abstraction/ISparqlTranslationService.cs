using DataSpecificationNavigatorBackend.Model;

namespace DataSpecificationNavigatorBackend.BusinessCoreLayer.Abstraction;

public interface ISparqlTranslationService
{
	string TranslateSubstructure(DataSpecificationSubstructure substructure);
}
