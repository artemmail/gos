using System.Collections.Generic;
using System.Xml.Serialization;

namespace Zakupki.EF2020;

/// <summary>
/// Unified KTRU classifier entry describing product characteristics.
/// </summary>
public class Ktru
{
    [XmlElement(ElementName = "code", Namespace = Ns.Base)]
    public string? Code { get; set; }

    [XmlElement(ElementName = "name", Namespace = Ns.Base)]
    public string? Name { get; set; }

    [XmlElement(ElementName = "versionId", Namespace = Ns.Base)]
    public string? VersionId { get; set; }

    [XmlElement(ElementName = "versionNumber", Namespace = Ns.Base)]
    public int VersionNumber { get; set; }

    [XmlElement(ElementName = "characteristics", Namespace = Ns.Common)]
    public KtruCharacteristics? Characteristics { get; set; }

    [XmlElement(ElementName = "OKPD2", Namespace = Ns.Common)]
    public Okpd2? Okpd2 { get; set; }
}

public class KtruCharacteristics
{
    [XmlElement(ElementName = "characteristicsUsingReferenceInfo", Namespace = Ns.Common)]
    public List<KtruCharacteristicReferenceInfo>? ReferenceInfos { get; set; }

    [XmlElement(ElementName = "characteristicsUsingTextForm", Namespace = Ns.Common)]
    public List<KtruCharacteristicTextForm>? TextForms { get; set; }

    [XmlElement(ElementName = "addCharacteristicInfoReason", Namespace = Ns.Common)]
    public string? AddCharacteristicInfoReason { get; set; }
}

public class KtruCharacteristicReferenceInfo
{
    [XmlElement(ElementName = "sid", Namespace = Ns.Common)]
    public string? Sid { get; set; }

    [XmlElement(ElementName = "externalSid", Namespace = Ns.Common)]
    public string? ExternalSid { get; set; }

    [XmlElement(ElementName = "code", Namespace = Ns.Common)]
    public string? Code { get; set; }

    [XmlElement(ElementName = "name", Namespace = Ns.Common)]
    public string? Name { get; set; }

    [XmlElement(ElementName = "type", Namespace = Ns.Common)]
    public string? Type { get; set; }

    [XmlElement(ElementName = "kind", Namespace = Ns.Common)]
    public string? Kind { get; set; }

    [XmlElement(ElementName = "values", Namespace = Ns.Common)]
    public KtruCharacteristicReferenceValues? Values { get; set; }

    [XmlElement(ElementName = "characteristicsFillingInstruction", Namespace = Ns.Common)]
    public CharacteristicsFillingInstruction? CharacteristicsFillingInstruction { get; set; }
}

public class KtruCharacteristicReferenceValues
{
    [XmlElement(ElementName = "value", Namespace = Ns.Common)]
    public List<KtruCharacteristicReferenceValue>? Items { get; set; }

    [XmlElement(ElementName = "valueSet", Namespace = Ns.Common)]
    public List<KtruCharacteristicReferenceValueSet>? Sets { get; set; }
}

public class KtruCharacteristicReferenceValue
{
    [XmlElement(ElementName = "sid", Namespace = Ns.Common)]
    public string? Sid { get; set; }

    [XmlElement(ElementName = "externalSid", Namespace = Ns.Common)]
    public string? ExternalSid { get; set; }

    [XmlElement(ElementName = "OKEI", Namespace = Ns.Common)]
    public Okei? Okei { get; set; }

    [XmlElement(ElementName = "valueFormat", Namespace = Ns.Common)]
    public string? ValueFormat { get; set; }

    [XmlElement(ElementName = "rangeSet", Namespace = Ns.Common)]
    public KtruCharacteristicRangeSet? RangeSet { get; set; }

    [XmlElement(ElementName = "qualityDescription", Namespace = Ns.Common)]
    public string? QualityDescription { get; set; }

    [XmlElement(ElementName = "valueSet", Namespace = Ns.Common)]
    public KtruCharacteristicReferenceValueSet? ValueSet { get; set; }
}

public class KtruCharacteristicReferenceValueSet
{
    [XmlElement(ElementName = "OKEI", Namespace = Ns.Common)]
    public Okei? Okei { get; set; }

    [XmlElement(ElementName = "valueFormat", Namespace = Ns.Common)]
    public string? ValueFormat { get; set; }

    [XmlElement(ElementName = "rangeSet", Namespace = Ns.Common)]
    public KtruCharacteristicRangeSet? RangeSet { get; set; }

    [XmlElement(ElementName = "qualityDescription", Namespace = Ns.Common)]
    public string? QualityDescription { get; set; }

    [XmlElement(ElementName = "concreteValue", Namespace = Ns.Common)]
    public List<string>? ConcreteValues { get; set; }

    [XmlElement(ElementName = "value", Namespace = Ns.Common)]
    public List<KtruCharacteristicReferenceValue>? Items { get; set; }
}

public class KtruCharacteristicRangeSet
{
    [XmlElement(ElementName = "valueRange", Namespace = Ns.Common)]
    public List<KtruCharacteristicValueRange>? Items { get; set; }

    [XmlElement(ElementName = "outValueRange", Namespace = Ns.Common)]
    public List<KtruCharacteristicOutValueRange>? OutValueRanges { get; set; }
}

public class KtruCharacteristicValueRange
{
    [XmlElement(ElementName = "minMathNotation", Namespace = Ns.Common)]
    public string? MinMathNotation { get; set; }

    [XmlElement(ElementName = "min", Namespace = Ns.Common)]
    public string? Min { get; set; }

    [XmlElement(ElementName = "maxMathNotation", Namespace = Ns.Common)]
    public string? MaxMathNotation { get; set; }

    [XmlElement(ElementName = "max", Namespace = Ns.Common)]
    public string? Max { get; set; }
}

public class KtruCharacteristicOutValueRange
{
    [XmlElement(ElementName = "minMathNotation", Namespace = Ns.Common)]
    public string? MinMathNotation { get; set; }

    [XmlElement(ElementName = "min", Namespace = Ns.Common)]
    public string? Min { get; set; }

    [XmlElement(ElementName = "maxMathNotation", Namespace = Ns.Common)]
    public string? MaxMathNotation { get; set; }

    [XmlElement(ElementName = "max", Namespace = Ns.Common)]
    public string? Max { get; set; }
}

public class CharacteristicsFillingInstruction
{
    [XmlElement(ElementName = "code", Namespace = Ns.Base)]
    public string? Code { get; set; }

    [XmlElement(ElementName = "name", Namespace = Ns.Base)]
    public string? Name { get; set; }
}

public class KtruCharacteristicTextForm
{
    [XmlElement(ElementName = "sid", Namespace = Ns.Common)]
    public string? Sid { get; set; }

    [XmlElement(ElementName = "externalSid", Namespace = Ns.Common)]
    public string? ExternalSid { get; set; }

    [XmlElement(ElementName = "code", Namespace = Ns.Common)]
    public string? Code { get; set; }

    [XmlElement(ElementName = "name", Namespace = Ns.Common)]
    public string? Name { get; set; }

    [XmlElement(ElementName = "type", Namespace = Ns.Common)]
    public string? Type { get; set; }

    [XmlElement(ElementName = "characteristicsFillingInstruction", Namespace = Ns.Common)]
    public CharacteristicsFillingInstruction? CharacteristicsFillingInstruction { get; set; }

    [XmlElement(ElementName = "values", Namespace = Ns.Common)]
    public KtruCharacteristicTextValues? Values { get; set; }
}

public class KtruCharacteristicTextValues
{
    [XmlElement(ElementName = "value", Namespace = Ns.Common)]
    public List<KtruCharacteristicTextValue>? Items { get; set; }
}

public class KtruCharacteristicTextValue
{
    [XmlElement(ElementName = "sid", Namespace = Ns.Common)]
    public string? Sid { get; set; }

    [XmlElement(ElementName = "externalSid", Namespace = Ns.Common)]
    public string? ExternalSid { get; set; }

    [XmlElement(ElementName = "qualityDescription", Namespace = Ns.Common)]
    public string? QualityDescription { get; set; }

    [XmlElement(ElementName = "OKEI", Namespace = Ns.Common)]
    public Okei? Okei { get; set; }

    [XmlElement(ElementName = "rangeSet", Namespace = Ns.Common)]
    public KtruCharacteristicRangeSet? RangeSet { get; set; }

    [XmlElement(ElementName = "valueSet", Namespace = Ns.Common)]
    public KtruCharacteristicReferenceValueSet? ValueSet { get; set; }
}

public class Okpd2
{
    [XmlElement(ElementName = "OKPDCode", Namespace = Ns.Base)]
    public string? Code { get; set; }

    [XmlElement(ElementName = "OKPDName", Namespace = Ns.Base)]
    public string? Name { get; set; }

    [XmlElement(ElementName = "characteristics", Namespace = Ns.Common)]
    public Okpd2Characteristics? Characteristics { get; set; }
}

public class Okpd2Characteristics
{
    [XmlElement(ElementName = "characteristicsUsingTextForm", Namespace = Ns.Common)]
    public List<Okpd2CharacteristicTextForm>? TextForms { get; set; }
}

public class Okpd2CharacteristicTextForm
{
    [XmlElement(ElementName = "sid", Namespace = Ns.Common)]
    public string? Sid { get; set; }

    [XmlElement(ElementName = "externalSid", Namespace = Ns.Common)]
    public string? ExternalSid { get; set; }

    [XmlElement(ElementName = "name", Namespace = Ns.Common)]
    public string? Name { get; set; }

    [XmlElement(ElementName = "type", Namespace = Ns.Common)]
    public string? Type { get; set; }

    [XmlElement(ElementName = "characteristicsFillingInstruction", Namespace = Ns.Common)]
    public CharacteristicsFillingInstruction? CharacteristicsFillingInstruction { get; set; }

    [XmlElement(ElementName = "values", Namespace = Ns.Common)]
    public Okpd2CharacteristicValues? Values { get; set; }
}

public class Okpd2CharacteristicValues
{
    [XmlElement(ElementName = "value", Namespace = Ns.Common)]
    public List<Okpd2CharacteristicValue>? Items { get; set; }

    [XmlElement(ElementName = "valueSet", Namespace = Ns.Common)]
    public List<Okpd2CharacteristicValueSet>? Sets { get; set; }
}

public class Okpd2CharacteristicValue
{
    [XmlElement(ElementName = "sid", Namespace = Ns.Common)]
    public string? Sid { get; set; }

    [XmlElement(ElementName = "externalSid", Namespace = Ns.Common)]
    public string? ExternalSid { get; set; }

    [XmlElement(ElementName = "qualityDescription", Namespace = Ns.Common)]
    public string? QualityDescription { get; set; }

    [XmlElement(ElementName = "OKEI", Namespace = Ns.Common)]
    public Okei? Okei { get; set; }

    [XmlElement(ElementName = "rangeSet", Namespace = Ns.Common)]
    public Okpd2CharacteristicRangeSet? RangeSet { get; set; }

    [XmlElement(ElementName = "valueSet", Namespace = Ns.Common)]
    public Okpd2CharacteristicValueSet? ValueSet { get; set; }
}

public class Okpd2CharacteristicValueSet
{
    [XmlElement(ElementName = "OKEI", Namespace = Ns.Common)]
    public Okei? Okei { get; set; }

    [XmlElement(ElementName = "valueFormat", Namespace = Ns.Common)]
    public string? ValueFormat { get; set; }

    [XmlElement(ElementName = "rangeSet", Namespace = Ns.Common)]
    public Okpd2CharacteristicRangeSet? RangeSet { get; set; }

    [XmlElement(ElementName = "qualityDescription", Namespace = Ns.Common)]
    public string? QualityDescription { get; set; }

    [XmlElement(ElementName = "value", Namespace = Ns.Common)]
    public List<Okpd2CharacteristicValue>? Items { get; set; }

    [XmlElement(ElementName = "concreteValue", Namespace = Ns.Common)]
    public List<string>? ConcreteValues { get; set; }
}

public class Okpd2CharacteristicRangeSet
{
    [XmlElement(ElementName = "valueRange", Namespace = Ns.Common)]
    public List<Okpd2CharacteristicValueRange>? Items { get; set; }

    [XmlElement(ElementName = "outValueRange", Namespace = Ns.Common)]
    public List<Okpd2CharacteristicOutValueRange>? OutValueRanges { get; set; }
}

public class Okpd2CharacteristicValueRange
{
    [XmlElement(ElementName = "minMathNotation", Namespace = Ns.Common)]
    public string? MinMathNotation { get; set; }

    [XmlElement(ElementName = "min", Namespace = Ns.Common)]
    public string? Min { get; set; }

    [XmlElement(ElementName = "maxMathNotation", Namespace = Ns.Common)]
    public string? MaxMathNotation { get; set; }

    [XmlElement(ElementName = "max", Namespace = Ns.Common)]
    public string? Max { get; set; }
}

public class Okpd2CharacteristicOutValueRange
{
    [XmlElement(ElementName = "minMathNotation", Namespace = Ns.Common)]
    public string? MinMathNotation { get; set; }

    [XmlElement(ElementName = "min", Namespace = Ns.Common)]
    public string? Min { get; set; }

    [XmlElement(ElementName = "maxMathNotation", Namespace = Ns.Common)]
    public string? MaxMathNotation { get; set; }

    [XmlElement(ElementName = "max", Namespace = Ns.Common)]
    public string? Max { get; set; }
}

/// <summary>
/// Reference to an OKEI measurement unit.
/// </summary>
public class Okei
{
    [XmlElement(ElementName = "code", Namespace = Ns.Base)]
    public string? Code { get; set; }

    [XmlElement(ElementName = "nationalCode", Namespace = Ns.Base)]
    public string? NationalCode { get; set; }

    [XmlElement(ElementName = "name", Namespace = Ns.Base)]
    public string? Name { get; set; }
}
