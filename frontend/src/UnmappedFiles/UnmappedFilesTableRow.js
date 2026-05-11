import PropTypes from 'prop-types';
import React, { Component } from 'react';
import BookQuality from 'Book/BookQuality';
import FileDetailsModal from 'BookFile/FileDetailsModal';
import Form from 'Components/Form/Form';
import FormGroup from 'Components/Form/FormGroup';
import FormInputGroup from 'Components/Form/FormInputGroup';
import FormLabel from 'Components/Form/FormLabel';
import Label from 'Components/Label';
import Button from 'Components/Link/Button';
import IconButton from 'Components/Link/IconButton';
import Link from 'Components/Link/Link';
import ConfirmModal from 'Components/Modal/ConfirmModal';
import Modal from 'Components/Modal/Modal';
import ModalBody from 'Components/Modal/ModalBody';
import ModalContent from 'Components/Modal/ModalContent';
import ModalFooter from 'Components/Modal/ModalFooter';
import ModalHeader from 'Components/Modal/ModalHeader';
import RelativeDateCellConnector from 'Components/Table/Cells/RelativeDateCellConnector';
import VirtualTableRowCell from 'Components/Table/Cells/VirtualTableRowCell';
import VirtualTableSelectCell from 'Components/Table/Cells/VirtualTableSelectCell';
import Popover from 'Components/Tooltip/Popover';
import { icons, inputTypes, kinds, tooltipPositions } from 'Helpers/Props';
import InteractiveImportModal from 'InteractiveImport/InteractiveImportModal';
import { getContributorEvidenceLabel, getContributorEvidenceReviewReason, getContributorEvidenceSourceLabel, renderContributorEvidenceSummary } from 'Utilities/ContributorEvidence/getContributorEvidenceDisplay';
import formatBytes from 'Utilities/Number/formatBytes';
import translate from 'Utilities/String/translate';
import { buildAddSearchLinks } from './unmappedAddSearchUtils';
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

function getDeepIdentifySuggestion(review) {
  return review?.suggestions?.find((item) => item.type === 'deepAudio');
}

function getSuggestionStatusLabel(status) {
  switch (status) {
    case 'queued':
      return 'Queued';
    case 'skipped':
      return 'Skipped';
    case 'extractingIntro':
      return 'Extracting intro clip';
    case 'introClipReady':
      return 'Intro clip ready';
    case 'sendingToProvider':
      return 'Sending to provider';
    case 'waitingForTranscription':
      return 'Waiting for transcription';
    case 'parsingTranscript':
      return 'Parsing transcript clues';
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

function isSuggestionReadyForConfirmation(suggestion) {
  return suggestion &&
    (suggestion.status === 'transcriptCaptured' || suggestion.status === 'suggested') &&
    (suggestion.validatedNarrator || suggestion.narrator);
}

function getDeepIdentifyStatus(review, isReprocessing) {
  const suggestion = getDeepIdentifySuggestion(review);

  if (suggestion) {
    return getSuggestionStatusLabel(suggestion.stage || suggestion.status);
  }

  if (isReprocessing) {
    return 'Queued';
  }

  return null;
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

  if (suggestion.stage) {
    details.push({
      label: 'Stage',
      detail: getSuggestionStatusLabel(suggestion.stage)
    });
  }

  if (suggestion.providerEndpoint || suggestion.providerModel || suggestion.providerStatusCode || suggestion.providerDurationMs != null) {
    details.push({
      label: 'Provider debug',
      detail: [
        suggestion.providerEndpoint,
        suggestion.providerModel && `model ${suggestion.providerModel}`,
        suggestion.providerStatusCode && `status ${suggestion.providerStatusCode}`,
        suggestion.providerDurationMs != null && `${suggestion.providerDurationMs} ms`
      ].filter(Boolean).join(' - ')
    });
  }

  if (suggestion.providerResponseExcerpt) {
    details.push({
      label: 'Provider response excerpt',
      detail: suggestion.providerResponseExcerpt
    });
  }

  if (suggestion.narrator) {
    details.push({
      label: 'Narrator evidence',
      detail: suggestion.narrator
    });
  }

  if (suggestion.narratorValidationDetail) {
    details.push({
      label: suggestion.narratorValidationStatus === 'validated' ? 'Validated narrator metadata' : 'Narrator metadata check',
      detail: suggestion.narratorValidationDetail
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

function getContributorEvidenceDetails(contributorEvidence) {
  const details = (contributorEvidence || []).map((item) => {
    return {
      label: getContributorEvidenceLabel(item),
      detail: renderContributorEvidenceSummary(item, styles.inlineAction),
      context: getContributorEvidenceReviewReason(item),
      summary: `${item.sourceLabel || getContributorEvidenceSourceLabel(item.source)}: ${item.displayName}`
    };
  });

  const narratorNames = (contributorEvidence || [])
    .filter((item) => item.role === 'narrator' && item.normalizedName)
    .reduce((acc, item) => {
      if (!acc[item.normalizedName]) {
        acc[item.normalizedName] = [];
      }

      acc[item.normalizedName].push(`${item.sourceLabel || getContributorEvidenceSourceLabel(item.source)}: ${item.displayName}`);

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

function getManualNarratorValue(contributorEvidence) {
  return (contributorEvidence || []).find((item) => item.role === 'narrator' && item.source === 'manual')?.displayName ||
    (contributorEvidence || []).find((item) => item.role === 'narrator')?.displayName ||
    '';
}

function getAudioPreviewUrl(audioPreviewUrl) {
  if (!audioPreviewUrl) {
    return null;
  }

  const separator = audioPreviewUrl.includes('?') ? '&' : '?';

  return `${window.Readarr.apiRoot}${audioPreviewUrl}${separator}apikey=${encodeURIComponent(window.Readarr.apiKey)}`;
}

function getReviewSuggestion(review) {
  return getDeepIdentifySuggestion(review) || review?.suggestions?.find((item) => item.status !== 'disabled');
}

function getRunningSttSuggestion(review, isReprocessing) {
  const existingSuggestion = getReviewSuggestion(review);

  if (existingSuggestion) {
    return existingSuggestion;
  }

  if (!isReprocessing) {
    return null;
  }

  return {
    type: 'deepAudio',
    provider: 'stt',
    status: 'queued',
    stage: 'queued',
    requiresManualConfirmation: true,
    explanation: 'Scan STT is queued or running for this unmapped file.',
    contextSummary: 'ReadAIrr will extract only the short configured intro clip, send it to the configured speech-to-text provider, then ask the configured LLM to suggest the narrator for user confirmation.',
    steps: [
      {
        kind: 'queued',
        label: 'Queued',
        detail: 'Scan STT was started from this row.'
      },
      {
        kind: 'polling',
        label: 'Waiting for live updates',
        detail: 'This modal will refresh as the background STT command persists extraction, provider, transcript, LLM, and validation steps.'
      }
    ]
  };
}

class UnmappedFilesTableRow extends Component {

  //
  // Lifecycle

  constructor(props, context) {
    super(props, context);

    this.state = {
      isDetailsModalOpen: false,
      isInteractiveImportModalOpen: false,
      isConfirmDeleteModalOpen: false,
      isContributorEvidenceModalOpen: false,
      isSuggestionReviewModalOpen: false,
      manualMatchSuggestion: null,
      contributorDisplayName: ''
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
    this.setState({
      isInteractiveImportModalOpen: true,
      manualMatchSuggestion: null
    });
  };

  onInteractiveImportModalClose = () => {
    this.setState({
      isInteractiveImportModalOpen: false,
      manualMatchSuggestion: null
    });
  };

  onRetryIdentifyPress = () => {
    this.props.retryUnmappedFile(this.getBookFileIds());
  };

  onAiReviewPress = () => {
    this.props.aiReviewUnmappedFile(this.getBookFileIds());
  };

  onDeepIdentifyPress = () => {
    this.setState({ isSuggestionReviewModalOpen: true });
    this.props.deepIdentifyUnmappedFile(this.getBookFileIds());
  };

  onClearSuggestionsPress = () => {
    this.props.clearUnmappedSuggestions(this.getBookFileIds());
  };

  onSuggestionReviewPress = () => {
    this.setState({ isSuggestionReviewModalOpen: true });
  };

  onSuggestionReviewModalClose = () => {
    this.setState({ isSuggestionReviewModalOpen: false });
  };

  onAcceptSuggestionPress = () => {
    const suggestion = getReviewSuggestion(this.props.review);
    const narrator = suggestion?.validatedNarrator || suggestion?.narrator;

    if (narrator) {
      this.props.setContributorEvidence(this.props.id, narrator);
    }

    this.setState({
      isSuggestionReviewModalOpen: false,
      isInteractiveImportModalOpen: true,
      manualMatchSuggestion: suggestion ?
        {
          ...suggestion,
          narrator,
          likelyEdition: suggestion.validatedEditionTitle || suggestion.likelyEdition,
          foreignEditionId: suggestion.validatedForeignEditionId
        } :
        null
    });
  };

  onContributorEvidencePress = () => {
    const {
      contributorEvidence,
      review
    } = this.props;

    this.setState({
      isContributorEvidenceModalOpen: true,
      contributorDisplayName: getManualNarratorValue(review?.contributorEvidence || contributorEvidence)
    });
  };

  onContributorEvidenceModalClose = () => {
    this.setState({
      isContributorEvidenceModalOpen: false,
      contributorDisplayName: ''
    });
  };

  onContributorEvidenceInputChange = ({ value }) => {
    this.setState({ contributorDisplayName: value });
  };

  onContributorEvidenceSavePress = () => {
    const displayName = this.state.contributorDisplayName.trim();

    if (!displayName) {
      return;
    }

    this.props.setContributorEvidence(this.props.id, displayName);
    this.onContributorEvidenceModalClose();
  };

  onMarkReviewedPress = () => {
    this.props.setUnmappedFileReviewed(this.getBookFileIds(), !this.props.reviewed);
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

  getBookFileIds = () => {
    return this.props.bookFileIds || [this.props.id];
  };

  //
  // Render

  render() {
    const {
      id,
      partCount,
      path,
      size,
      dateAdded,
      quality,
      reviewed,
      review,
      contributorEvidence,
      isReprocessing,
      columns,
      isSelected,
      onSelectedChange
    } = this.props;

    const folder = path.substring(0, Math.max(path.lastIndexOf('/'), path.lastIndexOf('\\')));

    const {
      isInteractiveImportModalOpen,
      isDetailsModalOpen,
      isConfirmDeleteModalOpen,
      isContributorEvidenceModalOpen,
      isSuggestionReviewModalOpen,
      manualMatchSuggestion,
      contributorDisplayName
    } = this.state;

    const reviewSuggestion = getRunningSttSuggestion(review, isReprocessing);
    const reviewAudioPreviewUrl = getAudioPreviewUrl(reviewSuggestion?.audioPreviewUrl);
    const reviewAddLinks = buildAddSearchLinks(review, reviewSuggestion, contributorEvidence);
    const addAuthorUrl = reviewAddLinks.addAuthorUrl;
    const addBookUrl = reviewAddLinks.addBookUrl;

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
                  {
                    partCount > 1 &&
                      <div className={styles.progressStatus}>
                        {partCount} grouped parts
                      </div>
                  }
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
              const deepIdentifyStatus = getDeepIdentifyStatus(review, isReprocessing);

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

                  {
                    deepIdentifyStatus &&
                      <div className={styles.progressStatus}>
                        Deep Identify: {deepIdentifyStatus}
                      </div>
                  }
                </VirtualTableRowCell>
              );
            }

            if (name === 'candidate') {
              const parsed = review?.parsed || {};
              const candidate = review?.candidate || {};
              const suggestion = review?.suggestions?.find((item) => item.status !== 'disabled') ||
                (isReprocessing ? {
                  type: 'deepAudio',
                  status: 'queued',
                  stage: 'queued',
                  explanation: 'Deep Identify Audio is running for this row.'
                } : null);
              const candidateTitle = candidate.bookTitle || parsed.book || 'No book candidate';
              const edition = candidate.editionTitle || candidate.editionFormat || candidate.editionLanguage;
              const audioPreviewUrl = getAudioPreviewUrl(suggestion?.audioPreviewUrl);
              const contributorEvidenceDetails = getContributorEvidenceDetails(review?.contributorEvidence || contributorEvidence);
              const candidateAddLinks = buildAddSearchLinks(review, suggestion, contributorEvidence);

              return (
                <VirtualTableRowCell
                  key={name}
                  className={styles[name]}
                >
                  <div className={styles.candidateContent}>
                    <div className={styles.candidateTitle}>
                      {candidateTitle}
                    </div>

                    <div className={styles.candidateSubline}>
                      {
                        edition &&
                          <span className={styles.candidateMeta}>
                            {edition}
                          </span>
                      }

                      {
                        suggestion &&
                          <Popover
                            anchor={
                              <span className={styles.suggestionMeta}>
                                {getSuggestionSummary(suggestion)}
                              </span>
                            }
                            title={`${getSuggestionSource(suggestion)} details`}
                            body={
                              <div className={styles.reasonList}>
                                {
                                  audioPreviewUrl &&
                                    <div className={styles.reason}>
                                      <div className={styles.reasonLabel}>
                                        Intro preview
                                      </div>

                                      <div className={styles.reasonDetail}>
                                        <audio
                                          controls={true}
                                          preload="none"
                                          src={audioPreviewUrl}
                                        />
                                      </div>
                                    </div>
                                }

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

                      {
                        suggestion &&
                          <Link
                            className={styles.inlineAction}
                            onPress={this.onSuggestionReviewPress}
                          >
                            Review
                          </Link>
                      }

                      {
                        contributorEvidenceDetails.length > 0 &&
                          <Popover
                            anchor={
                              <span className={styles.suggestionMeta}>
                                {contributorEvidenceDetails.map((item) => item.summary || item.detail).join(' - ')}
                              </span>
                            }
                            title="Narrator evidence"
                            body={
                              <div className={styles.reasonList}>
                                {
                                  contributorEvidenceDetails.map((detail, index) => {
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

                                        {
                                          detail.context &&
                                            <div className={styles.reasonContext}>
                                              {detail.context}
                                            </div>
                                        }
                                      </div>
                                    );
                                  })
                                }
                              </div>
                            }
                            position={tooltipPositions.LEFT}
                          />
                      }

                      {
                        candidateAddLinks.addAuthorUrl &&
                          <Link
                            className={styles.inlineAction}
                            to={candidateAddLinks.addAuthorUrl}
                          >
                            Add Author
                          </Link>
                      }

                      {
                        candidateAddLinks.addBookUrl &&
                          <Link
                            className={styles.inlineAction}
                            to={candidateAddLinks.addBookUrl}
                          >
                            Add Book
                          </Link>
                      }
                    </div>
                  </div>
                </VirtualTableRowCell>
              );
            }

            if (name === 'candidateAuthor') {
              const parsed = review?.parsed || {};
              const candidate = review?.candidate || {};
              const candidateAuthor = candidate.authorName || parsed.author || 'No author candidate';

              return (
                <VirtualTableRowCell
                  key={name}
                  className={styles[name]}
                >
                  <div className={styles.candidateContent}>
                    <div className={styles.candidateTitle}>
                      {candidateAuthor}
                    </div>

                    <div className={styles.candidateSubline}>
                      {
                        addAuthorUrl &&
                          <Link
                            className={styles.inlineAction}
                            to={addAuthorUrl}
                          >
                            Add Author
                          </Link>
                      }
                    </div>
                  </div>
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
                    title="Scan STT intro"
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
                    name={icons.EDIT}
                    title="Edit narrator evidence"
                    isSpinning={isReprocessing}
                    onPress={this.onContributorEvidencePress}
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
          acceptedSuggestion={manualMatchSuggestion}
          acceptedPath={path}
          onModalClose={this.onInteractiveImportModalClose}
        />

        <FileDetailsModal
          isOpen={isDetailsModalOpen}
          onModalClose={this.onDetailsModalClose}
          id={id}
          showAudioTagEditor={false}
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

        <Modal
          isOpen={isContributorEvidenceModalOpen}
          onModalClose={this.onContributorEvidenceModalClose}
        >
          <ModalContent onModalClose={this.onContributorEvidenceModalClose}>
            <ModalHeader>
              Edit Narrator Evidence
            </ModalHeader>

            <ModalBody>
              <Form>
                <FormGroup>
                  <FormLabel>
                    Narrator
                  </FormLabel>

                  <FormInputGroup
                    type={inputTypes.TEXT}
                    name="contributorDisplayName"
                    value={contributorDisplayName}
                    helpText="Stores a manual narrator evidence row for review. Existing AI and transcript evidence is kept for comparison."
                    onChange={this.onContributorEvidenceInputChange}
                  />
                </FormGroup>
              </Form>
            </ModalBody>

            <ModalFooter>
              <Button
                onPress={this.onContributorEvidenceModalClose}
              >
                Cancel
              </Button>

              <Button
                isDisabled={!contributorDisplayName.trim()}
                onPress={this.onContributorEvidenceSavePress}
              >
                Save
              </Button>
            </ModalFooter>
          </ModalContent>
        </Modal>

        <Modal
          isOpen={isSuggestionReviewModalOpen}
          onModalClose={this.onSuggestionReviewModalClose}
        >
          <ModalContent onModalClose={this.onSuggestionReviewModalClose}>
            <ModalHeader>
              Scan STT Review
            </ModalHeader>

            <ModalBody>
              {
                reviewSuggestion ?
                  <div className={styles.reviewModal}>
                    <div className={styles.reviewSummary}>
                      <div className={styles.reviewTitle}>
                        {getSuggestionSummary(reviewSuggestion)}
                      </div>

                      <div className={styles.reviewMeta}>
                        {[
                          reviewSuggestion.likelyAuthor,
                          reviewSuggestion.likelyBook,
                          reviewSuggestion.narrator && `Narrator: ${reviewSuggestion.narrator}`,
                          reviewSuggestion.confidence != null && `${reviewSuggestion.confidence}% confidence`
                        ].filter(Boolean).join(' - ')}
                      </div>
                    </div>

                    {
                      reviewAudioPreviewUrl &&
                        <div className={styles.reviewSection}>
                          <div className={styles.reviewSectionTitle}>
                            Intro clip
                          </div>

                          <audio
                            controls={true}
                            preload="none"
                            src={reviewAudioPreviewUrl}
                          />
                        </div>
                    }

                    {
                      reviewSuggestion.steps?.length > 0 &&
                        <div className={styles.reviewSection}>
                          <div className={styles.reviewSectionTitle}>
                            Live STT session log
                          </div>

                          <div className={styles.stepList}>
                            {
                              reviewSuggestion.steps.map((step, index) => {
                                return (
                                  <div
                                    key={index}
                                    className={styles.step}
                                  >
                                    <div className={styles.stepLabel}>
                                      {step.label}
                                    </div>

                                    <div className={styles.stepDetail}>
                                      {step.detail}
                                    </div>
                                  </div>
                                );
                              })
                            }
                          </div>
                        </div>
                    }

                    {
                      (reviewSuggestion.transcript || reviewSuggestion.transcriptExcerpt) &&
                        <div className={styles.reviewSection}>
                          <div className={styles.reviewSectionTitle}>
                            Transcript
                          </div>

                          <pre className={styles.transcript}>
                            {reviewSuggestion.transcript || reviewSuggestion.transcriptExcerpt}
                            {reviewSuggestion.transcriptIsTruncated ? '\n\n[Transcript truncated for review storage]' : ''}
                          </pre>
                        </div>
                    }

                    {
                      getSuggestionDetails(reviewSuggestion).length > 0 &&
                        <div className={styles.reviewSection}>
                          <div className={styles.reviewSectionTitle}>
                            Details
                          </div>

                          <div className={styles.reasonList}>
                            {
                              getSuggestionDetails(reviewSuggestion).map((detail, index) => {
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
                        </div>
                    }

                    {
                      addAuthorUrl &&
                        <div className={styles.reviewSection}>
                          <div className={styles.reviewSectionTitle}>
                            Missing author
                          </div>

                          <div className={styles.reasonDetail}>
                            Add the likely author using the existing add flow, then return here and retry identify or manual match.
                          </div>
                        </div>
                    }

                    {
                      addBookUrl &&
                        <div className={styles.reviewSection}>
                          <div className={styles.reviewSectionTitle}>
                            Missing book
                          </div>

                          <div className={styles.reasonDetail}>
                            Add the likely book using the existing add flow, then return here and retry identify or manual match.
                          </div>
                        </div>
                    }
                  </div> :
                  <div>
                    No suggestion is available for review.
                  </div>
              }
            </ModalBody>

            <ModalFooter>
              <Button onPress={this.onSuggestionReviewModalClose}>
                Close
              </Button>

              <Button
                isDisabled={!isSuggestionReadyForConfirmation(reviewSuggestion)}
                onPress={this.onAcceptSuggestionPress}
              >
                Confirm Narrator and Manual Match
              </Button>

              {
                addAuthorUrl &&
                  <Button
                    to={addAuthorUrl}
                  >
                    Add Author
                  </Button>
              }

              {
                addBookUrl &&
                  <Button
                    to={addBookUrl}
                  >
                    Add Book
                  </Button>
              }
            </ModalFooter>
          </ModalContent>
        </Modal>

      </>
    );
  }

}

UnmappedFilesTableRow.propTypes = {
  id: PropTypes.number.isRequired,
  bookFileIds: PropTypes.arrayOf(PropTypes.number),
  partCount: PropTypes.number,
  path: PropTypes.string.isRequired,
  size: PropTypes.number.isRequired,
  quality: PropTypes.object,
  dateAdded: PropTypes.string.isRequired,
  reviewed: PropTypes.bool.isRequired,
  review: PropTypes.object,
  contributorEvidence: PropTypes.arrayOf(PropTypes.object),
  isReprocessing: PropTypes.bool,
  columns: PropTypes.arrayOf(PropTypes.object).isRequired,
  isSelected: PropTypes.bool,
  onSelectedChange: PropTypes.func.isRequired,
  deleteUnmappedFile: PropTypes.func.isRequired,
  retryUnmappedFile: PropTypes.func.isRequired,
  aiReviewUnmappedFile: PropTypes.func.isRequired,
  deepIdentifyUnmappedFile: PropTypes.func.isRequired,
  clearUnmappedSuggestions: PropTypes.func.isRequired,
  setContributorEvidence: PropTypes.func.isRequired,
  setUnmappedFileReviewed: PropTypes.func.isRequired
};

UnmappedFilesTableRow.defaultProps = {
  reviewed: false,
  isReprocessing: false
};

export default UnmappedFilesTableRow;
