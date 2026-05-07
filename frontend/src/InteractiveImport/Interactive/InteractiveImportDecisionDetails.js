import PropTypes from 'prop-types';
import React from 'react';
import { getManualImportDecision } from './manualImportReviewContext';
import styles from './InteractiveImportDecisionDetails.css';

function InteractiveImportDecisionDetails(props) {
  const decision = getManualImportDecision(props);
  const {
    reviewContext,
    reasons
  } = decision;

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
  importError: PropTypes.string
};

export default InteractiveImportDecisionDetails;
