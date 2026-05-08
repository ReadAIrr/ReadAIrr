import classNames from 'classnames';
import PropTypes from 'prop-types';
import React, { Component } from 'react';
import BookFormats from 'Book/BookFormats';
import BookQuality from 'Book/BookQuality';
import IndexerFlags from 'Book/IndexerFlags';
import FileDetails from 'BookFile/FileDetails';
import Form from 'Components/Form/Form';
import FormGroup from 'Components/Form/FormGroup';
import FormInputGroup from 'Components/Form/FormInputGroup';
import FormLabel from 'Components/Form/FormLabel';
import Icon from 'Components/Icon';
import Button from 'Components/Link/Button';
import IconButton from 'Components/Link/IconButton';
import ConfirmModal from 'Components/Modal/ConfirmModal';
import Modal from 'Components/Modal/Modal';
import ModalBody from 'Components/Modal/ModalBody';
import ModalContent from 'Components/Modal/ModalContent';
import ModalFooter from 'Components/Modal/ModalFooter';
import ModalHeader from 'Components/Modal/ModalHeader';
import TableRowCell from 'Components/Table/Cells/TableRowCell';
import TableRowCellButton from 'Components/Table/Cells/TableRowCellButton';
import TableSelectCell from 'Components/Table/Cells/TableSelectCell';
import TableRow from 'Components/Table/TableRow';
import Popover from 'Components/Tooltip/Popover';
import Tooltip from 'Components/Tooltip/Tooltip';
import { icons, inputTypes, kinds, sizes, tooltipPositions } from 'Helpers/Props';
import SelectAuthorModal from 'InteractiveImport/Author/SelectAuthorModal';
import SelectBookModal from 'InteractiveImport/Book/SelectBookModal';
import SelectIndexerFlagsModal from 'InteractiveImport/IndexerFlags/SelectIndexerFlagsModal';
import SelectQualityModal from 'InteractiveImport/Quality/SelectQualityModal';
import SelectReleaseGroupModal from 'InteractiveImport/ReleaseGroup/SelectReleaseGroupModal';
import formatBytes from 'Utilities/Number/formatBytes';
import translate from 'Utilities/String/translate';
import InteractiveImportDecisionDetails from './InteractiveImportDecisionDetails';
import InteractiveImportRowCellPlaceholder from './InteractiveImportRowCellPlaceholder';
import { getManualImportDecision } from './manualImportReviewContext';
import styles from './InteractiveImportRow.css';

function getManualNarratorEvidence(contributorEvidence) {
  return (contributorEvidence || []).find((item) => item.role === 'narrator' && item.source === 'manual');
}

class InteractiveImportRow extends Component {

  //
  // Lifecycle

  constructor(props, context) {
    super(props, context);

    this.state = {
      isDetailsModalOpen: false,
      isSelectAuthorModalOpen: false,
      isSelectBookModalOpen: false,
      isSelectReleaseGroupModalOpen: false,
      isSelectQualityModalOpen: false,
      isSelectIndexerFlagsModalOpen: false,
      isContributorEvidenceModalOpen: false,
      contributorDisplayName: ''
    };
  }

  componentDidMount() {
    const {
      id,
      author,
      book,
      foreignEditionId,
      quality,
      size
    } = this.props;

    if (
      author &&
      book != null &&
      foreignEditionId &&
      quality &&
      size > 0
    ) {
      this.props.onSelectedChange({ id, value: true });
    }
  }

  componentDidUpdate(prevProps) {
    const {
      id,
      author,
      book,
      foreignEditionId,
      quality,
      isSelected,
      onValidRowChange
    } = this.props;

    if (
      prevProps.author === author &&
      prevProps.book === book &&
      prevProps.foreignEditionId === foreignEditionId &&
      prevProps.quality === quality &&
      prevProps.isSelected === isSelected
    ) {
      return;
    }

    const isValid = !!(
      author &&
      book &&
      foreignEditionId &&
      quality
    );

    if (isSelected && !isValid) {
      onValidRowChange(id, false);
    } else {
      onValidRowChange(id, true);
    }
  }

  //
  // Control

  selectRowAfterChange = (value) => {
    const {
      id,
      isSelected
    } = this.props;

    if (!isSelected && value === true) {
      this.props.onSelectedChange({ id, value });
    }
  };

  //
  // Listeners

  onDetailsPress = () => {
    this.setState({ isDetailsModalOpen: true });
  };

  onDetailsModalClose = () => {
    this.setState({ isDetailsModalOpen: false });
  };

  onSelectAuthorPress = () => {
    this.setState({ isSelectAuthorModalOpen: true });
  };

  onSelectBookPress = () => {
    this.setState({ isSelectBookModalOpen: true });
  };

  onSelectReleaseGroupPress = () => {
    this.setState({ isSelectReleaseGroupModalOpen: true });
  };

  onSelectQualityPress = () => {
    this.setState({ isSelectQualityModalOpen: true });
  };

  onSelectIndexerFlagsPress = () => {
    this.setState({ isSelectIndexerFlagsModalOpen: true });
  };

  onSelectAuthorModalClose = (changed) => {
    this.setState({ isSelectAuthorModalOpen: false });
    this.selectRowAfterChange(changed);
  };

  onSelectBookModalClose = (changed) => {
    this.setState({ isSelectBookModalOpen: false });
    this.selectRowAfterChange(changed);
  };

  onSelectReleaseGroupModalClose = (changed) => {
    this.setState({ isSelectReleaseGroupModalOpen: false });
    this.selectRowAfterChange(changed);
  };

  onSelectQualityModalClose = (changed) => {
    this.setState({ isSelectQualityModalOpen: false });
    this.selectRowAfterChange(changed);
  };

  onSelectIndexerFlagsModalClose = (changed) => {
    this.setState({ isSelectIndexerFlagsModalOpen: false });
    this.selectRowAfterChange(changed);
  };

  onContributorEvidencePress = () => {
    const manualEvidence = getManualNarratorEvidence(this.props.review?.contributorEvidence);

    this.setState({
      isContributorEvidenceModalOpen: true,
      contributorDisplayName: manualEvidence?.displayName || ''
    });
  };

  onContributorEvidenceModalClose = () => {
    this.setState({ isContributorEvidenceModalOpen: false });
  };

  onContributorEvidenceInputChange = ({ value }) => {
    this.setState({ contributorDisplayName: value });
  };

  onContributorEvidenceSavePress = () => {
    const displayName = this.state.contributorDisplayName.trim();
    const bookFileId = this.props.review?.bookFileId;

    if (!displayName || !bookFileId) {
      return;
    }

    this.props.onSetContributorEvidencePress({
      id: this.props.id,
      bookFileId,
      role: 'narrator',
      displayName
    });
    this.onContributorEvidenceModalClose();
  };

  //
  // Render

  render() {
    const {
      id,
      allowAuthorChange,
      path,
      author,
      book,
      quality,
      releaseGroup,
      size,
      customFormats,
      indexerFlags,
      columns,
      additionalFile,
      isSelected,
      isReprocessing,
      isImporting,
      importStatus,
      importError,
      onSelectedChange,
      audioTags,
      review
    } = this.props;

    const {
      isDetailsModalOpen,
      isSelectAuthorModalOpen,
      isSelectBookModalOpen,
      isSelectReleaseGroupModalOpen,
      isSelectQualityModalOpen,
      isSelectIndexerFlagsModalOpen,
      isContributorEvidenceModalOpen,
      contributorDisplayName
    } = this.state;

    const authorName = author ? author.authorName : '';
    let bookTitle = '';
    if (book) {
      bookTitle = book.disambiguation ? `${book.title} (${book.disambiguation})` : book.title;
    }

    const showAuthorPlaceholder = isSelected && !author;
    const showBookNumberPlaceholder = !isReprocessing && isSelected && !!author && !book;
    const showReleaseGroupPlaceholder = isSelected && !releaseGroup;
    const showQualityPlaceholder = isSelected && !quality;
    const showIndexerFlagsPlaceholder = isSelected && !indexerFlags;

    const pathCellContents = (
      <div onClick={this.onDetailsPress}>
        {path}
      </div>
    );

    const pathCell = additionalFile ? (
      <Tooltip
        anchor={pathCellContents}
        tooltip='This file is already in your library for a release you are currently importing'
        position={tooltipPositions.TOP}
      />
    ) : pathCellContents;

    const fileDetails = (
      <FileDetails
        audioTags={audioTags}
        filename={path}
      />
    );

    const isIndexerFlagsColumnVisible = columns.find((c) => c.name === 'indexerFlags')?.isVisible ?? false;
    const decision = getManualImportDecision(this.props);
    let statusCell = null;

    if (isImporting) {
      statusCell = (
        <Popover
          anchor={
            <Icon
              name={icons.SPINNER}
              kind={kinds.PRIMARY}
              isSpinning={true}
            />
          }
          title={translate('Importing')}
          body={importStatus || importError || 'Import command queued or running.'}
          position={tooltipPositions.LEFT}
          canFlip={false}
        />
      );
    } else if (importError) {
      statusCell = (
        <Popover
          anchor={
            <Icon
              name={icons.DANGER}
              kind={kinds.DANGER}
            />
          }
          title={translate('ReleaseRejected')}
          body={importError}
          position={tooltipPositions.LEFT}
          canFlip={false}
        />
      );
    } else if (decision.reasons.length) {
      statusCell = (
        <Popover
          anchor={
            <Icon
              name={icons.DANGER}
              kind={kinds.DANGER}
            />
          }
          title="Import decision"
          body={<InteractiveImportDecisionDetails {...this.props} />}
          position={tooltipPositions.LEFT}
          canFlip={false}
        />
      );
    } else {
      statusCell = (
        <Popover
          anchor={
            <Icon
              name={icons.CHECK_CIRCLE}
              kind={kinds.SUCCESS}
            />
          }
          title="Import decision"
          body={<InteractiveImportDecisionDetails {...this.props} />}
          position={tooltipPositions.LEFT}
          canFlip={false}
        />
      );
    }

    return (
      <TableRow
        className={classNames(
          additionalFile && styles.additionalFile,
          isImporting && styles.importing,
          importError && styles.importError
        )}
      >
        <TableSelectCell
          id={id}
          isSelected={isSelected}
          isDisabled={isImporting}
          onSelectedChange={onSelectedChange}
        />

        <TableRowCell
          className={styles.path}
          title={path}
        >
          {pathCell}
        </TableRowCell>

        <TableRowCellButton
          isDisabled={!allowAuthorChange || isImporting}
          title={allowAuthorChange ? translate('AllowAuthorChangeClickToChangeAuthor') : undefined}
          onPress={this.onSelectAuthorPress}
        >
          {
            showAuthorPlaceholder ? <InteractiveImportRowCellPlaceholder /> : authorName
          }
        </TableRowCellButton>

        <TableRowCellButton
          isDisabled={!author || isImporting}
          title={author ? translate('AuthorClickToChangeBook') : undefined}
          onPress={this.onSelectBookPress}
        >
          {
            showBookNumberPlaceholder ? <InteractiveImportRowCellPlaceholder /> : bookTitle
          }
        </TableRowCellButton>

        <TableRowCellButton
          isDisabled={isImporting}
          title={translate('ClickToChangeReleaseGroup')}
          onPress={this.onSelectReleaseGroupPress}
        >
          {
            showReleaseGroupPlaceholder ?
              <InteractiveImportRowCellPlaceholder
                isOptional={true}
              /> :
              releaseGroup
          }
        </TableRowCellButton>

        <TableRowCellButton
          className={styles.quality}
          isDisabled={isImporting}
          title={translate('ClickToChangeQuality')}
          onPress={this.onSelectQualityPress}
        >
          {
            showQualityPlaceholder &&
              <InteractiveImportRowCellPlaceholder />
          }

          {
            !showQualityPlaceholder && !!quality &&
              <BookQuality
                className={styles.label}
                quality={quality}
              />
          }
        </TableRowCellButton>

        <TableRowCell>
          {formatBytes(size)}
        </TableRowCell>

        <TableRowCell>
          {
            customFormats?.length ?
              <Popover
                anchor={
                  <Icon name={icons.INTERACTIVE} />
                }
                title={translate('Formats')}
                body={
                  <div className={styles.customFormatTooltip}>
                    <BookFormats formats={customFormats} />
                  </div>
                }
                position={tooltipPositions.LEFT}
              /> :
              null
          }
        </TableRowCell>

        {isIndexerFlagsColumnVisible ? (
          <TableRowCellButton
            isDisabled={isImporting}
            title={translate('ClickToChangeIndexerFlags')}
            onPress={this.onSelectIndexerFlagsPress}
          >
            {showIndexerFlagsPlaceholder ? (
              <InteractiveImportRowCellPlaceholder isOptional={true} />
            ) : (
              <>
                {indexerFlags ? (
                  <Popover
                    anchor={<Icon name={icons.FLAG} kind={kinds.PRIMARY} />}
                    title={translate('IndexerFlags')}
                    body={<IndexerFlags indexerFlags={indexerFlags} />}
                    position={tooltipPositions.LEFT}
                  />
                ) : null}
              </>
            )}
          </TableRowCellButton>
        ) : null}

        <TableRowCell>
          {statusCell}
          {
            review?.canEditContributorEvidence &&
              <IconButton
                name={icons.EDIT}
                title="Edit narrator evidence"
                isSpinning={isReprocessing}
                onPress={this.onContributorEvidencePress}
              />
          }
        </TableRowCell>

        <ConfirmModal
          isOpen={isDetailsModalOpen}
          title={translate('FileDetails')}
          message={fileDetails}
          size={sizes.LARGE}
          kind={kinds.DEFAULT}
          hideCancelButton={true}
          confirmLabel={translate('Close')}
          onConfirm={this.onDetailsModalClose}
          onCancel={this.onDetailsModalClose}
        />

        <SelectAuthorModal
          isOpen={isSelectAuthorModalOpen}
          ids={[id]}
          onModalClose={this.onSelectAuthorModalClose}
        />

        <SelectBookModal
          isOpen={isSelectBookModalOpen}
          ids={[id]}
          authorId={author && author.id}
          onModalClose={this.onSelectBookModalClose}
        />

        <SelectReleaseGroupModal
          isOpen={isSelectReleaseGroupModalOpen}
          ids={[id]}
          releaseGroup={releaseGroup ?? ''}
          onModalClose={this.onSelectReleaseGroupModalClose}
        />

        <SelectQualityModal
          isOpen={isSelectQualityModalOpen}
          ids={[id]}
          qualityId={quality ? quality.quality.id : 0}
          proper={quality ? quality.revision.version > 1 : false}
          real={quality ? quality.revision.real > 0 : false}
          onModalClose={this.onSelectQualityModalClose}
        />

        <SelectIndexerFlagsModal
          isOpen={isSelectIndexerFlagsModalOpen}
          ids={[id]}
          indexerFlags={indexerFlags ?? 0}
          onModalClose={this.onSelectIndexerFlagsModalClose}
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
                    helpText="Stores manual narrator evidence for this persisted unmapped file. AI and transcript evidence is kept for comparison."
                    onChange={this.onContributorEvidenceInputChange}
                  />
                </FormGroup>
              </Form>
            </ModalBody>

            <ModalFooter>
              <Button onPress={this.onContributorEvidenceModalClose}>
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
      </TableRow>
    );
  }

}

InteractiveImportRow.propTypes = {
  id: PropTypes.number.isRequired,
  allowAuthorChange: PropTypes.bool.isRequired,
  path: PropTypes.string.isRequired,
  author: PropTypes.object,
  book: PropTypes.object,
  foreignEditionId: PropTypes.string,
  releaseGroup: PropTypes.string,
  quality: PropTypes.object,
  size: PropTypes.number.isRequired,
  customFormats: PropTypes.arrayOf(PropTypes.object),
  indexerFlags: PropTypes.number.isRequired,
  rejections: PropTypes.arrayOf(PropTypes.object).isRequired,
  columns: PropTypes.arrayOf(PropTypes.object).isRequired,
  audioTags: PropTypes.object.isRequired,
  additionalFile: PropTypes.bool.isRequired,
  isReprocessing: PropTypes.bool,
  isImporting: PropTypes.bool,
  importStatus: PropTypes.string,
  importError: PropTypes.string,
  review: PropTypes.object,
  isSelected: PropTypes.bool,
  onSelectedChange: PropTypes.func.isRequired,
  onValidRowChange: PropTypes.func.isRequired,
  onSetContributorEvidencePress: PropTypes.func.isRequired
};

export default InteractiveImportRow;
