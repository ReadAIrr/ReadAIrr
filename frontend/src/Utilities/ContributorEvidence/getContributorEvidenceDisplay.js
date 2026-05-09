import React from 'react';
import Link from 'Components/Link/Link';

export function getContributorEvidenceSourceLabel(source) {
  switch (source) {
    case 'manual':
      return 'Manual';
    case 'providerMetadata':
      return 'Provider metadata';
    case 'aiReview':
      return 'AI review';
    case 'sttTranscript':
      return 'STT transcript';
    default:
      return source || 'Unknown source';
  }
}

function getConfidenceLabel(item) {
  if (item.confidenceLabel) {
    return item.confidenceLabel;
  }

  if (item.source === 'providerMetadata') {
    return 'Provider-confirmed';
  }

  if (item.source === 'manual') {
    return 'Manual evidence';
  }

  if (item.confidence == null) {
    return 'Needs review';
  }

  if (item.confidence >= 85) {
    return 'High-confidence review';
  }

  if (item.confidence >= 60) {
    return 'Medium-confidence review';
  }

  return 'Needs review';
}

export function getNarratorDiscoveryUrl(item) {
  if (item.discoveryUrl) {
    return item.discoveryUrl;
  }

  if (item.role !== 'narrator' || !item.displayName) {
    return null;
  }

  return `/narrators?term=${encodeURIComponent(item.displayName)}`;
}

export function getContributorEvidenceLabel(item) {
  const role = item.role || 'Contributor';

  return `${role.charAt(0).toUpperCase()}${role.slice(1)} evidence`;
}

export function getContributorEvidenceSummary(item) {
  const source = item.sourceLabel || getContributorEvidenceSourceLabel(item.source);
  const confidenceLabel = getConfidenceLabel(item);
  const confidence = item.confidence == null ? '' : ` (${item.confidence}%)`;

  return `${source}: ${item.displayName}${confidence} - ${confidenceLabel}`;
}

export function renderContributorEvidenceSummary(item, className) {
  const discoveryUrl = getNarratorDiscoveryUrl(item);
  const summary = getContributorEvidenceSummary(item);

  if (!discoveryUrl) {
    return summary;
  }

  return (
    <>
      {summary}
      {' '}
      <Link
        className={className}
        to={discoveryUrl}
      >
        Find narrator works
      </Link>
    </>
  );
}

export function getContributorEvidenceReviewReason(item) {
  if (item.reviewOnlyReason) {
    return item.reviewOnlyReason;
  }

  if (item.role === 'narrator') {
    return 'Review-only narrator evidence. Use the narrator page to find related works; this evidence does not write tags or metadata automatically.';
  }

  return null;
}
