import _ from 'lodash';
import PropTypes from 'prop-types';
import React, { Component } from 'react';
import Alert from 'Components/Alert';
import SelectInput from 'Components/Form/SelectInput';
import Button from 'Components/Link/Button';
import SpinnerButton from 'Components/Link/SpinnerButton';
import LoadingIndicator from 'Components/Loading/LoadingIndicator';
import ConfirmModal from 'Components/Modal/ConfirmModal';
import Modal from 'Components/Modal/Modal';
import ModalBody from 'Components/Modal/ModalBody';
import ModalContent from 'Components/Modal/ModalContent';
import ModalFooter from 'Components/Modal/ModalFooter';
import ModalHeader from 'Components/Modal/ModalHeader';
import Table from 'Components/Table/Table';
import TableBody from 'Components/Table/TableBody';
import { kinds } from 'Helpers/Props';
import createAjaxRequest from 'Utilities/createAjaxRequest';
import getErrorMessage from 'Utilities/Object/getErrorMessage';
import hasDifferentItems from 'Utilities/Object/hasDifferentItems';
import translate from 'Utilities/String/translate';
import getSelectedIds from 'Utilities/Table/getSelectedIds';
import removeOldSelectedState from 'Utilities/Table/removeOldSelectedState';
import selectAll from 'Utilities/Table/selectAll';
import toggleSelected from 'Utilities/Table/toggleSelected';
import BookFileEditorRow from './BookFileEditorRow';
import styles from './BookFileEditorTableContent.css';

class BookFileEditorTableContent extends Component {

  //
  // Lifecycle

  constructor(props, context) {
    super(props, context);

    this.state = {
      allSelected: false,
      allUnselected: false,
      lastToggled: null,
      selectedState: {},
      isConfirmDeleteModalOpen: false,
      isAudioTagTemplateModalOpen: false,
      isAudioTagTemplateFetching: false,
      isAudioTagTemplateWriting: false,
      audioTagTemplateError: null,
      audioTagTemplatePreview: null,
      audioTagTemplates: [],
      selectedAudioTagTemplate: 'readarr'
    };
  }

  componentDidUpdate(prevProps) {
    if (hasDifferentItems(prevProps.items, this.props.items)) {
      this.setState((state) => {
        return removeOldSelectedState(state, prevProps.items);
      });
    }
  }

  //
  // Control

  getSelectedIds = () => {
    const ids = getSelectedIds(this.state.selectedState);
    return ids;
  };

  //
  // Listeners

  onSelectAllChange = ({ value }) => {
    this.setState(selectAll(this.state.selectedState, value));
  };

  onSelectedChange = ({ id, value, shiftKey = false }) => {
    this.setState((state) => {
      return toggleSelected(state, this.props.items, id, value, shiftKey);
    });
  };

  onDeletePress = () => {
    this.setState({ isConfirmDeleteModalOpen: true });
  };

  onConfirmDelete = () => {
    this.setState({ isConfirmDeleteModalOpen: false });
    this.props.onDeletePress(this.getSelectedIds());
  };

  onConfirmDeleteModalClose = () => {
    this.setState({ isConfirmDeleteModalOpen: false });
  };

  onQualityChange = ({ value }) => {
    const selectedIds = this.getSelectedIds();

    if (!selectedIds.length) {
      return;
    }

    this.props.onQualityChange(selectedIds, parseInt(value));
  };

  onAudioTagTemplatesPress = () => {
    this.setState({ isAudioTagTemplateModalOpen: true }, () => {
      this.previewAudioTagTemplate(false);
    });
  };

  onAudioTagTemplateModalClose = () => {
    this.setState({
      isAudioTagTemplateModalOpen: false,
      audioTagTemplateError: null
    });
  };

  onAudioTagTemplateChange = ({ value }) => {
    this.setState({ selectedAudioTagTemplate: value }, () => {
      this.previewAudioTagTemplate(false);
    });
  };

  previewAudioTagTemplate = (write = false) => {
    if (this.state.isAudioTagTemplateWriting) {
      return;
    }

    const selectedIds = this.getSelectedIds();

    if (!selectedIds.length) {
      return;
    }

    this.setState({
      isAudioTagTemplateFetching: !write,
      isAudioTagTemplateWriting: write,
      audioTagTemplateError: null
    });

    const promise = createAjaxRequest({
      url: write ? '/bookFile/audioTag/templates' : '/bookFile/audioTag/templates/preview',
      method: write ? 'PUT' : 'POST',
      dataType: 'json',
      data: JSON.stringify({
        bookFileIds: selectedIds,
        template: this.state.selectedAudioTagTemplate
      })
    }).request;

    promise.done((preview) => {
      this.setState({
        isAudioTagTemplateFetching: false,
        isAudioTagTemplateWriting: false,
        audioTagTemplatePreview: preview,
        audioTagTemplates: preview.templates || []
      });
    });

    promise.fail((xhr) => {
      this.setState({
        isAudioTagTemplateFetching: false,
        isAudioTagTemplateWriting: false,
        audioTagTemplateError: xhr
      });
    });
  };

  getFileName(path, fallback) {
    if (!path) {
      return fallback;
    }

    return path.split(/[\\/]/).pop();
  }

  renderTemplateFileDiffs(file) {
    if (!file.changes.length) {
      return (
        <div className={styles.noTemplateChanges}>
          No field changes.
        </div>
      );
    }

    return (
      <div className={styles.templateDiffs}>
        {
          file.changes.map((change) => {
            return (
              <div
                key={change.field}
                className={styles.templateDiff}
              >
                <div className={styles.templateDiffField}>
                  {change.field}
                </div>
                <div className={styles.templateDiffValues}>
                  <span className={styles.templateDiffValue}>
                    {change.currentValue || 'Empty'}
                  </span>
                  <span className={styles.templateDiffArrow}>-&gt;</span>
                  <span className={styles.templateDiffValue}>
                    {change.proposedValue || 'Empty'}
                  </span>
                </div>
              </div>
            );
          })
        }
      </div>
    );
  }

  renderTemplateFileWarnings(file) {
    const warnings = [
      file.warning,
      ...(file.writeWarnings || [])
    ].filter(Boolean);

    if (!warnings.length) {
      return null;
    }

    return (
      <ul className={styles.templateWarnings}>
        {
          warnings.map((warning) => {
            return (
              <li key={warning}>{warning}</li>
            );
          })
        }
      </ul>
    );
  }

  renderAudioTagTemplateModal() {
    const {
      isAudioTagTemplateModalOpen,
      isAudioTagTemplateFetching,
      isAudioTagTemplateWriting,
      audioTagTemplateError,
      audioTagTemplatePreview,
      audioTagTemplates,
      selectedAudioTagTemplate
    } = this.state;

    return (
      <Modal
        isOpen={isAudioTagTemplateModalOpen}
        onModalClose={this.onAudioTagTemplateModalClose}
      >
        <ModalContent onModalClose={this.onAudioTagTemplateModalClose}>
          <ModalHeader>
            Audio Tag Templates
          </ModalHeader>

          <ModalBody>
            {
              audioTagTemplateError ?
                <Alert kind={kinds.DANGER}>
                  {getErrorMessage(audioTagTemplateError, 'Unable to preview audio tag template')}
                </Alert> :
                null
            }

            {
              audioTagTemplatePreview?.warning ?
                <Alert kind={kinds.WARNING}>
                  {audioTagTemplatePreview.warning}
                </Alert> :
                null
            }

            <div className={styles.templateControls}>
              <SelectInput
                name="selectedAudioTagTemplate"
                value={selectedAudioTagTemplate}
                values={(audioTagTemplates.length ? audioTagTemplates : [{ name: 'readarr', label: 'ReadAIrr metadata' }]).map((template) => {
                  return {
                    key: template.name,
                    value: template.label
                  };
                })}
                isDisabled={isAudioTagTemplateFetching || isAudioTagTemplateWriting}
                onChange={this.onAudioTagTemplateChange}
              />

              <Button
                isDisabled={isAudioTagTemplateFetching || isAudioTagTemplateWriting}
                onPress={() => this.previewAudioTagTemplate(false)}
              >
                Preview
              </Button>
            </div>

            {
              isAudioTagTemplateFetching ?
                <LoadingIndicator /> :
                null
            }

            {
              audioTagTemplatePreview ?
                <div>
                  <div className={styles.templateSummary}>
                    {audioTagTemplatePreview.changedFiles} of {audioTagTemplatePreview.totalFiles} selected files would change.
                  </div>

                  <table className={styles.templatePreviewTable}>
                    <thead>
                      <tr>
                        <th>File</th>
                        <th>Changes</th>
                        <th>Warnings</th>
                      </tr>
                    </thead>
                    <tbody>
                      {
                        audioTagTemplatePreview.files.map((file) => {
                          const fileName = this.getFileName(file.path, file.bookFileId);

                          return (
                            <tr key={file.bookFileId}>
                              <td>{fileName}</td>
                              <td>
                                {this.renderTemplateFileDiffs(file)}
                              </td>
                              <td>
                                {this.renderTemplateFileWarnings(file)}
                              </td>
                            </tr>
                          );
                        })
                      }
                    </tbody>
                  </table>
                </div> :
                null
            }
          </ModalBody>

          <ModalFooter>
            <Button onPress={this.onAudioTagTemplateModalClose}>
              Close
            </Button>

            <SpinnerButton
              kind={kinds.PRIMARY}
              isSpinning={isAudioTagTemplateWriting}
              isDisabled={!audioTagTemplatePreview || !audioTagTemplatePreview.changedFiles || isAudioTagTemplateFetching || isAudioTagTemplateWriting}
              onPress={() => this.previewAudioTagTemplate(true)}
            >
              Apply Template
            </SpinnerButton>
          </ModalFooter>
        </ModalContent>
      </Modal>
    );
  }

  //
  // Render

  render() {
    const {
      isDeleting,
      isFetching,
      isPopulated,
      error,
      items,
      qualities,
      dispatchDeleteBookFile,
      ...otherProps
    } = this.props;

    const {
      allSelected,
      allUnselected,
      selectedState,
      isConfirmDeleteModalOpen
    } = this.state;

    const qualityOptions = _.reduceRight(qualities, (acc, quality) => {
      acc.push({
        key: quality.id,
        value: quality.name
      });

      return acc;
    }, [{ key: 'selectQuality', value: translate('SelectQuality'), isDisabled: true }]);

    const hasSelectedFiles = this.getSelectedIds().length > 0;

    return (
      <div>
        {
          isFetching && !isPopulated ?
            <LoadingIndicator /> :
            null
        }

        {
          !isFetching && error ?
            <Alert kind={kinds.DANGER}>{error}</Alert> :
            null
        }

        {
          isPopulated && !items.length ?
            <div className={styles.blankpad}>
              No book files to manage.
            </div> :
            null
        }

        {
          isPopulated && items.length ?
            <div
              className={styles.filesTable}
            >
              <Table
                selectAll={true}
                allSelected={allSelected}
                allUnselected={allUnselected}
                onSelectAllChange={this.onSelectAllChange}
                {...otherProps}
              >
                <TableBody>
                  {
                    items.map((item) => {
                      return (
                        <BookFileEditorRow
                          key={item.id}
                          isSelected={selectedState[item.id]}
                          {...item}
                          onSelectedChange={this.onSelectedChange}
                          deleteBookFile={dispatchDeleteBookFile}
                        />
                      );
                    })
                  }
                </TableBody>
              </Table>
            </div> :
            null
        }

        {
          isPopulated && items.length ? (
            <div className={styles.actions}>
              <SpinnerButton
                kind={kinds.DANGER}
                isSpinning={isDeleting}
                isDisabled={!hasSelectedFiles}
                onPress={this.onDeletePress}
              >
                {translate('Delete')}
              </SpinnerButton>

              <div className={styles.selectInput}>
                <SelectInput
                  name="quality"
                  value="selectQuality"
                  values={qualityOptions}
                  isDisabled={!hasSelectedFiles}
                  onChange={this.onQualityChange}
                />
              </div>

              <Button
                isDisabled={!hasSelectedFiles}
                onPress={this.onAudioTagTemplatesPress}
              >
                Audio Tag Templates
              </Button>
            </div>
          ) : null
        }

        <ConfirmModal
          isOpen={isConfirmDeleteModalOpen}
          kind={kinds.DANGER}
          title={translate('DeleteSelectedBookFiles')}
          message={translate('DeleteSelectedBookFilesMessageText')}
          confirmLabel={translate('Delete')}
          onConfirm={this.onConfirmDelete}
          onCancel={this.onConfirmDeleteModalClose}
        />

        {this.renderAudioTagTemplateModal()}
      </div>
    );
  }
}

BookFileEditorTableContent.propTypes = {
  isDeleting: PropTypes.bool.isRequired,
  isFetching: PropTypes.bool.isRequired,
  isPopulated: PropTypes.bool.isRequired,
  error: PropTypes.object,
  items: PropTypes.arrayOf(PropTypes.object).isRequired,
  qualities: PropTypes.arrayOf(PropTypes.object).isRequired,
  onDeletePress: PropTypes.func.isRequired,
  onQualityChange: PropTypes.func.isRequired,
  dispatchDeleteBookFile: PropTypes.func.isRequired
};

export default BookFileEditorTableContent;
