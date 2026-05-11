function cleanRawValue(value) {
  return (value || '').toString().replace(/[;\r\n]+/g, ' ').trim();
}

function addRawPart(parts, key, value) {
  const cleanValue = cleanRawValue(value);

  if (cleanValue) {
    parts.push(`${key}=${cleanValue}`);
  }
}

function getAcceptedNarratorEvidencePayload(suggestion, displayName, confirmedFrom = 'identificationReview') {
  const narrator = (displayName || suggestion?.validatedNarrator || suggestion?.narrator || '').trim();

  if (!narrator) {
    return null;
  }

  const rawParts = [];
  addRawPart(rawParts, 'confirmedFrom', confirmedFrom);
  addRawPart(rawParts, 'narrator', narrator);
  addRawPart(rawParts, 'suggestedNarrator', suggestion?.narrator);
  addRawPart(rawParts, 'validatedNarrator', suggestion?.validatedNarrator);
  addRawPart(rawParts, 'author', suggestion?.likelyAuthor || suggestion?.suggestedAuthor);
  addRawPart(rawParts, 'book', suggestion?.likelyBook || suggestion?.suggestedBook);
  addRawPart(rawParts, 'edition', suggestion?.validatedEditionTitle || suggestion?.likelyEdition || suggestion?.suggestedEdition);
  addRawPart(rawParts, 'foreignEditionId', suggestion?.validatedForeignEditionId || suggestion?.foreignEditionId);
  addRawPart(rawParts, 'confidence', suggestion?.confidence || suggestion?.suggestionConfidence);
  addRawPart(rawParts, 'validation', suggestion?.narratorValidationStatus);

  return {
    role: 'narrator',
    displayName: narrator,
    confidence: suggestion?.confidence || suggestion?.suggestionConfidence,
    rawValue: rawParts.join(';')
  };
}

export default getAcceptedNarratorEvidencePayload;
