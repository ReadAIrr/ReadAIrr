import PropTypes from 'prop-types';
import React from 'react';
import { getManualImportDecision } from './manualImportReviewContext';
import styles from './InteractiveImportDecisionDetails.css';

function getContributorEvidenceSourceLabel(source) {
  switch (source) {
    case 'manual':
      return 'Manual';
    case 'aiReview':
      return 'AI Review';
    case 'sttTranscript':
      return 'STT transcript';
    default:
      return source || 'Unknown source';
  }
}

function getContributorEvidenceDetails(contributorEvidence) {
  const details = (contributorEvidence || []).map((item) => {
    const confidence = item.confidence == null ? '' : ` (${item.confidence}%)`;
    const source = getContributorEvidenceSourceLabel(item.source);

    return {
      label: `${item.role || 'Contributor'} evidence`,
      detail: `${source}: ${item.displayName}${confidence}`
    };
  });

  const narratorNames = (contributorEvidence || [])
    .filter((item) => item.role === 'narrator' && item.normalizedName)
    .reduce((acc, item) => {
      if (!acc[item.normalizedName]) {
        acc[item.normalizedName] = [];
      }

      acc[item.normalizedName].push(`${getContributorEvidenceSourceLabel(item.source)}: ${item.displayName}`);

      return acc;
    }, {});

  const narratorNameKeys = Object.keys(narratorNames);

  if (narratorNameKeys.length > 1) {
    details.push({
      label: 'Narrator evidence conflict',
      detail: narratorNameKeys.map((key) => narratorNames[key][0]).join(' vs ')
    });
  }

  return details;
}

function getSuggestionDetails(suggestions) {
  return (suggestions || []).reduce((acc, suggestion) => {
    const source = suggestion.type === 'deepAudio' ? 'Deep Identify' : 'AI Review';

    (suggestion.evidence || []).forEach((item) => {
      acc.push({
        label: `${source}: ${item.label}`,
        detail: item.detail
      });
    });

    (suggestion.warnings || []).forEach((item) => {
      acc.push({
        label: `${source}: ${item.label}`,
        detail: item.detail
      });
    });

    return acc;
  }, []);
}

function InteractiveImportDecisionDetails(props) {
  const decision = getManualImportDecision(props);
  const {
    reviewContext,
    reasons
  } = decision;
  const review = props.review || {};
  const hints = review.hints || [];
  const contributorEvidenceDetails = getContributorEvidenceDetails(review.contributorEvidence);
  const suggestionDetails = getSuggestionDetails(review.suggestions);
  const reviewDetails = [
    ...hints,
    ...contributorEvidenceDetails,
    ...suggestionDetails,
    review.contributorEvidenceEditReason ? {
      label: review.canEditContributorEvidence ? 'Manual evidence editing' : 'Manual evidence editing unavailable',
      detail: review.contributorEvidenceEditReason
    } : null
  ];
  const filteredReviewDetails = reviewDetails.filter(Boolean);

  return (
    <div className={styles.decisionSummary}>
      <div className={styles.candidate}>
        <div className={styles.candidateTitle}>
          {reviewContext.matched.authorName || reviewContext.tags.authorTitle || 'No author candidate'}
        </div>

        <div className={styles.candidateMeta}>
          {reviewContext.matched.bookTitle || reviewContext.tags.bookTitle || reviewContext.tags.title || 'No book candidate'}
          {
            reviewContext.tags.year > 0 &&
              ` (${reviewContext.tags.year})`
          }
        </div>
      </div>

      {
        reasons.length > 0 ?
          <ul className={styles.reasonList}>
            {
              reasons.map((reason, index) => {
                return (
                  <li
                    key={index}
                    className={styles.reason}
                  >
                    <div className={styles.reasonLabel}>
                      {reason.label}
                    </div>

                    <div className={styles.reasonDetail}>
                      {reason.detail}
                    </div>
                  </li>
                );
              })
            }
          </ul> :
          <div className={styles.reasonDetail}>
            Ready to import.
          </div>
      }

      {
        filteredReviewDetails.length > 0 &&
          <div className={styles.reviewDetails}>
            <div className={styles.reviewDetailsTitle}>
              Review evidence
            </div>

            <ul className={styles.reasonList}>
              {
                filteredReviewDetails.map((item, index) => {
                  return (
                    <li
                      key={index}
                      className={styles.reason}
                    >
                      <div className={styles.reasonLabel}>
                        {item.label}
                      </div>

                      <div className={styles.reasonDetail}>
                        {item.detail}
                      </div>
                    </li>
                  );
                })
              }
            </ul>
          </div>
      }
    </div>
  );
}

InteractiveImportDecisionDetails.propTypes = {
  author: PropTypes.object,
  book: PropTypes.object,
  foreignEditionId: PropTypes.string,
  quality: PropTypes.object,
  releaseGroup: PropTypes.string,
  rejections: PropTypes.arrayOf(PropTypes.object).isRequired,
  audioTags: PropTypes.object.isRequired,
  review: PropTypes.object,
  importError: PropTypes.string
};

export default InteractiveImportDecisionDetails;
