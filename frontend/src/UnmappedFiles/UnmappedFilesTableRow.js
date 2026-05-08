import PropTypes from 'prop-types';
import React, { Component } from 'react';
import BookQuality from 'Book/BookQuality';
import FileDetailsModal from 'BookFile/FileDetailsModal';
import Label from 'Components/Label';
import IconButton from 'Components/Link/IconButton';
import ConfirmModal from 'Components/Modal/ConfirmModal';
import RelativeDateCellConnector from 'Components/Table/Cells/RelativeDateCellConnector';
import VirtualTableRowCell from 'Components/Table/Cells/VirtualTableRowCell';
import VirtualTableSelectCell from 'Components/Table/Cells/VirtualTableSelectCell';
import Popover from 'Components/Tooltip/Popover';
import { icons, kinds, tooltipPositions } from 'Helpers/Props';
import InteractiveImportModal from 'InteractiveImport/InteractiveImportModal';
import formatBytes from 'Utilities/Number/formatBytes';
import translate from 'Utilities/String/translate';
import styles from './UnmappedFilesTableRow.css';

function getStatusKind(status) {
  if (status === 'ready') {
    return kinds.SUCCESS;
  }

  if (status === 'reviewed') {
    return kinds.DEFAULT;
  }

  if (status === 'lowConfidence' || status === 'metadataMismatch') {
    return kinds.WARNING;
  }

  return kinds.DANGER;
}

function getSuggestionSource(suggestion) {
  return suggestion.type === 'deepAudio' ? 'Deep identify' : 'AI review';
}

function getSuggestionStatusLabel(status) {
  switch (status) {
    case 'disabled':
      return 'Not configured';
    case 'extractionFailed':
      return 'Intro extraction failed';
    case 'transcriptionFailed':
      return 'Transcription failed';
    case 'transcriptCaptured':
      return 'Transcript captured';
    case 'providerReady':
      return 'Provider ready';
    case 'suggested':
      return 'Suggestion ready';
    case 'failed':
      return 'Review failed';
    case 'unavailable':
      return 'No suggestion returned';
    default:
      return status || 'Pending review';
  }
}

function getSuggestionSummary(suggestion) {
  const statusLabel = getSuggestionStatusLabel(suggestion.status);
  const source = getSuggestionSource(suggestion);

  if (suggestion.status === 'transcriptCaptured') {
    const likely = [
      suggestion.likelyAuthor || 'Unknown author',
      suggestion.likelyBook || 'Unknown book'
    ].join(' - ');

    return `${source}${suggestion.isStale ? ' stale' : ''}: ${statusLabel} - ${likely}`;
  }

  if (suggestion.status === 'suggested') {
    return `${source}${suggestion.isStale ? ' stale' : ''}: ${suggestion.likelyAuthor || 'Unknown author'} - ${suggestion.likelyBook || 'Unknown book'}`;
  }

  return `${source}${suggestion.isStale ? ' stale' : ''}: ${statusLabel}`;
}

function getSuggestionDetails(suggestion) {
  const details = [];

  if (suggestion.explanation) {
    details.push({
      label: getSuggestionStatusLabel(suggestion.status),
      detail: suggestion.explanation
    });
  }

  if (suggestion.contextSummary) {
    details.push({
      label: 'Context',
      detail: suggestion.contextSummary
    });
  }

  if (suggestion.narrator) {
    details.push({
      label: 'Narrator evidence',
      detail: suggestion.narrator
    });
  }

  if (suggestion.evidence?.length) {
    suggestion.evidence.forEach((item) => {
      details.push({
        label: item.label,
        detail: item.detail
      });
    });
  }

  if (suggestion.warnings?.length) {
    suggestion.warnings.forEach((item) => {
      details.push({
        label: item.label,
        detail: item.detail
      });
    });
  }

  if (suggestion.transcriptExcerpt) {
    details.push({
      label: 'Transcript excerpt',
      detail: suggestion.transcriptExcerpt
    });
  }

  if (suggestion.isStale) {
    details.push({
      label: 'Stale suggestion',
      detail: 'The file path, size, or modified time changed after this suggestion was created.'
    });
  }

  return details;
}

class UnmappedFilesTableRow extends Component {

  //
  // Lifecycle

  constructor(props, context) {
    super(props, context);

    this.state = {
      isDetailsModalOpen: false,
      isInteractiveImportModalOpen: false,
      isConfirmDeleteModalOpen: false
    };
  }

  //
  // Listeners

  onDetailsPress = () => {
    this.setState({ isDetailsModalOpen: true });
  };

  onDetailsModalClose = () => {
    this.setState({ isDetailsModalOpen: false });
  };

  onInteractiveImportPress = () => {
    this.setState({ isInteractiveImportModalOpen: true });
  };

  onInteractiveImportModalClose = () => {
    this.setState({ isInteractiveImportModalOpen: false });
  };

  onRetryIdentifyPress = () => {
    this.props.retryUnmappedFile([this.props.id]);
  };

  onAiReviewPress = () => {
    this.props.aiReviewUnmappedFile([this.props.id]);
  };

  onDeepIdentifyPress = () => {
    this.props.deepIdentifyUnmappedFile([this.props.id]);
  };

  onClearSuggestionsPress = () => {
    this.props.clearUnmappedSuggestions([this.props.id]);
  };

  onMarkReviewedPress = () => {
    this.props.setUnmappedFileReviewed([this.props.id], !this.props.reviewed);
  };

  onDeleteFilePress = () => {
    this.setState({ isConfirmDeleteModalOpen: true });
  };

  onConfirmDelete = () => {
    this.setState({ isConfirmDeleteModalOpen: false });
    this.props.deleteUnmappedFile(this.props.id);
  };

  onConfirmDeleteModalClose = () => {
    this.setState({ isConfirmDeleteModalOpen: false });
  };

  //
  // Render

  render() {
    const {
      id,
      path,
      size,
      dateAdded,
      quality,
      reviewed,
      review,
      isReprocessing,
      columns,
      isSelected,
      onSelectedChange
    } = this.props;

    const folder = path.substring(0, Math.max(path.lastIndexOf('/'), path.lastIndexOf('\\')));

    const {
      isInteractiveImportModalOpen,
      isDetailsModalOpen,
      isConfirmDeleteModalOpen
    } = this.state;

    return (
      <>
        {
          columns.map((column) => {
            const {
              name,
              isVisible
            } = column;

            if (!isVisible) {
              return null;
            }

            if (name === 'select') {
              return (
                <VirtualTableSelectCell
                  inputClassName={styles.checkInput}
                  id={id}
                  key={name}
                  isSelected={isSelected}
                  isDisabled={false}
                  onSelectedChange={onSelectedChange}
                />
              );
            }

            if (name === 'path') {
              return (
                <VirtualTableRowCell
                  key={name}
                  className={styles[name]}
                >
                  {path}
                </VirtualTableRowCell>
              );
            }

            if (name === 'size') {
              return (
                <VirtualTableRowCell
                  key={name}
                  className={styles[name]}
                >
                  {formatBytes(size)}
                </VirtualTableRowCell>
              );
            }

            if (name === 'status') {
              const status = review?.status || 'needsReview';
              const reasons = review?.reasons || [];

              return (
                <VirtualTableRowCell
                  key={name}
                  className={styles[name]}
                >
                  <Popover
                    anchor={
                      <Label kind={getStatusKind(status)}>
                        {review?.statusLabel || 'Needs review'}
                      </Label>
                    }
                    title="Triage reasons"
                    body={
                      <div className={styles.reasonList}>
                        {
                          reasons.length ?
                            reasons.map((reason, index) => {
                              return (
                                <div
                                  key={index}
                                  className={styles.reason}
                                >
                                  <div className={styles.reasonLabel}>
                                    {reason.label}
                                  </div>

                                  <div className={styles.reasonDetail}>
                                    {reason.detail}
                                  </div>
                                </div>
                              );
                            }) :
                            <div className={styles.reasonDetail}>
                              No rejection details were returned.
                            </div>
                        }
                      </div>
                    }
                    position={tooltipPositions.LEFT}
                  />
                </VirtualTableRowCell>
              );
            }

            if (name === 'candidate') {
              const parsed = review?.parsed || {};
              const candidate = review?.candidate || {};
              const suggestion = review?.suggestions?.find((item) => item.status !== 'disabled');
              const candidateTitle = candidate.bookTitle || parsed.book || 'No book candidate';
              const candidateAuthor = candidate.authorName || parsed.author || 'No author candidate';
              const edition = candidate.editionTitle || candidate.editionFormat || candidate.editionLanguage;

              return (
                <VirtualTableRowCell
                  key={name}
                  className={styles[name]}
                >
                  <div className={styles.candidateTitle}>
                    {candidateTitle}
                  </div>

                  <div className={styles.candidateMeta}>
                    {candidateAuthor}
                    {
                      edition &&
                        ` - ${edition}`
                    }
                  </div>

                  {
                    suggestion &&
                      <Popover
                        anchor={
                          <div className={styles.suggestionMeta}>
                            {getSuggestionSummary(suggestion)}
                          </div>
                        }
                        title={`${getSuggestionSource(suggestion)} details`}
                        body={
                          <div className={styles.reasonList}>
                            {
                              getSuggestionDetails(suggestion).map((detail, index) => {
                                return (
                                  <div
                                    key={index}
                                    className={styles.reason}
                                  >
                                    <div className={styles.reasonLabel}>
                                      {detail.label}
                                    </div>

                                    <div className={styles.reasonDetail}>
                                      {detail.detail}
                                    </div>
                                  </div>
                                );
                              })
                            }
                          </div>
                        }
                        position={tooltipPositions.LEFT}
                      />
                  }
                </VirtualTableRowCell>
              );
            }

            if (name === 'confidence') {
              const confidence = review?.confidence;

              return (
                <VirtualTableRowCell
                  key={name}
                  className={styles[name]}
                >
                  {
                    confidence == null ?
                      <span className={styles.noConfidence}>--</span> :
                      <div className={styles.confidence}>
                        <div className={styles.confidenceValue}>
                          {confidence}%
                        </div>

                        <div className={styles.confidenceTrack}>
                          <div
                            className={styles.confidenceFill}
                            style={{ width: `${confidence}%` }}
                          />
                        </div>
                      </div>
                  }
                </VirtualTableRowCell>
              );
            }

            if (name === 'dateAdded') {
              return (
                <RelativeDateCellConnector
                  key={name}
                  className={styles[name]}
                  date={dateAdded}
                  component={VirtualTableRowCell}
                />
              );
            }

            if (name === 'quality') {
              return (
                <VirtualTableRowCell
                  key={name}
                  className={styles[name]}
                >
                  {
                    quality ?
                      <BookQuality
                        quality={quality}
                      /> :
                      <span className={styles.noConfidence}>--</span>
                  }
                </VirtualTableRowCell>
              );
            }

            if (name === 'actions') {
              return (
                <VirtualTableRowCell
                  key={name}
                  className={styles[name]}
                >
                  <IconButton
                    name={icons.INFO}
                    onPress={this.onDetailsPress}
                  />

                  <IconButton
                    name={icons.REFRESH}
                    isSpinning={isReprocessing}
                    onPress={this.onRetryIdentifyPress}
                  />

                  <IconButton
                    name={reviewed ? icons.RESTORE : icons.IGNORE}
                    title={reviewed ? 'Keep reviewing' : 'Mark reviewed'}
                    onPress={this.onMarkReviewedPress}
                  />

                  <IconButton
                    name={icons.INTERACTIVE}
                    onPress={this.onInteractiveImportPress}
                  />

                  <IconButton
                    name={icons.QUICK}
                    title="AI review"
                    isSpinning={isReprocessing}
                    onPress={this.onAiReviewPress}
                  />

                  <IconButton
                    name={icons.TRACK_FILE}
                    title="Deep identify audio"
                    isSpinning={isReprocessing}
                    onPress={this.onDeepIdentifyPress}
                  />

                  <IconButton
                    name={icons.CLEAR}
                    title="Clear suggestions"
                    isSpinning={isReprocessing}
                    onPress={this.onClearSuggestionsPress}
                  />

                  <IconButton
                    name={icons.DELETE}
                    onPress={this.onDeleteFilePress}
                  />

                </VirtualTableRowCell>
              );
            }

            return null;
          })
        }

        <InteractiveImportModal
          isOpen={isInteractiveImportModalOpen}
          folder={folder}
          showFilterExistingFiles={true}
          filterExistingFiles={false}
          showImportMode={false}
          showReplaceExistingFiles={false}
          replaceExistingFiles={false}
          onModalClose={this.onInteractiveImportModalClose}
        />

        <FileDetailsModal
          isOpen={isDetailsModalOpen}
          onModalClose={this.onDetailsModalClose}
          id={id}
        />

        <ConfirmModal
          isOpen={isConfirmDeleteModalOpen}
          kind={kinds.DANGER}
          title={translate('DeleteBookFile')}
          message={translate('DeleteBookFileMessageText', [path])}
          confirmLabel={translate('Delete')}
          onConfirm={this.onConfirmDelete}
          onCancel={this.onConfirmDeleteModalClose}
        />

      </>
    );
  }

}

UnmappedFilesTableRow.propTypes = {
  id: PropTypes.number.isRequired,
  path: PropTypes.string.isRequired,
  size: PropTypes.number.isRequired,
  quality: PropTypes.object,
  dateAdded: PropTypes.string.isRequired,
  reviewed: PropTypes.bool.isRequired,
  review: PropTypes.object,
  isReprocessing: PropTypes.bool,
  columns: PropTypes.arrayOf(PropTypes.object).isRequired,
  isSelected: PropTypes.bool,
  onSelectedChange: PropTypes.func.isRequired,
  deleteUnmappedFile: PropTypes.func.isRequired,
  retryUnmappedFile: PropTypes.func.isRequired,
  aiReviewUnmappedFile: PropTypes.func.isRequired,
  deepIdentifyUnmappedFile: PropTypes.func.isRequired,
  clearUnmappedSuggestions: PropTypes.func.isRequired,
  setUnmappedFileReviewed: PropTypes.func.isRequired
};

UnmappedFilesTableRow.defaultProps = {
  reviewed: false,
  isReprocessing: false
};

export default UnmappedFilesTableRow;
