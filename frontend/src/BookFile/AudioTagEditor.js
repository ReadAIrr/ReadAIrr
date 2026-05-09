import PropTypes from 'prop-types';
import React, { Component } from 'react';
import Alert from 'Components/Alert';
import FormGroup from 'Components/Form/FormGroup';
import FormInputGroup from 'Components/Form/FormInputGroup';
import FormLabel from 'Components/Form/FormLabel';
import Button from 'Components/Link/Button';
import LoadingIndicator from 'Components/Loading/LoadingIndicator';
import { inputTypes, kinds } from 'Helpers/Props';
import createAjaxRequest from 'Utilities/createAjaxRequest';
import getErrorMessage from 'Utilities/Object/getErrorMessage';
import styles from './AudioTagEditor.css';

const numericFields = [
  'track',
  'trackCount',
  'disc',
  'discCount',
  'year'
];

const fields = [
  { name: 'title', label: 'Title' },
  { name: 'book', label: 'Book / Album' },
  { name: 'author', label: 'Author / Album Artist' },
  { name: 'performers', label: 'Narrator / Performer' },
  { name: 'track', label: 'Track', type: inputTypes.NUMBER },
  { name: 'trackCount', label: 'Track Count', type: inputTypes.NUMBER },
  { name: 'disc', label: 'Disc', type: inputTypes.NUMBER },
  { name: 'discCount', label: 'Disc Count', type: inputTypes.NUMBER },
  { name: 'date', label: 'Date' },
  { name: 'year', label: 'Year', type: inputTypes.NUMBER },
  { name: 'publisher', label: 'Publisher' },
  { name: 'genres', label: 'Genres' },
  { name: 'comment', label: 'Comment', type: inputTypes.TEXT_AREA }
];

function normalizeValue(name, value) {
  if (numericFields.includes(name)) {
    const parsed = parseInt(value);
    return Number.isNaN(parsed) ? 0 : parsed;
  }

  return value;
}

class AudioTagEditor extends Component {

  constructor(props, context) {
    super(props, context);

    this.state = {
      isFetching: true,
      isPreviewing: false,
      isSaving: false,
      error: null,
      saveError: null,
      preview: null,
      proposed: null
    };
  }

  componentDidMount() {
    this.fetchPreview();
  }

  fetchPreview = () => {
    const promise = createAjaxRequest({
      url: `/bookFile/${this.props.id}/audioTag`,
      method: 'GET',
      dataType: 'json'
    }).request;

    promise.done((preview) => {
      this.setState({
        isFetching: false,
        error: null,
        preview,
        proposed: preview.proposed
      });
    });

    promise.fail((xhr) => {
      this.setState({
        isFetching: false,
        error: xhr
      });
    });
  };

  previewTags = (proposed) => {
    this.setState({ isPreviewing: true, saveError: null });

    const promise = createAjaxRequest({
      url: `/bookFile/${this.props.id}/audioTag/preview`,
      method: 'POST',
      dataType: 'json',
      data: JSON.stringify(proposed)
    }).request;

    promise.done((preview) => {
      this.setState({
        isPreviewing: false,
        preview,
        proposed: preview.proposed
      });
    });

    promise.fail((xhr) => {
      this.setState({
        isPreviewing: false,
        saveError: xhr
      });
    });
  };

  onInputChange = ({ name, value }) => {
    this.setState((state) => {
      return {
        proposed: {
          ...state.proposed,
          [name]: normalizeValue(name, value)
        }
      };
    });
  };

  onUseSuggestedPress = () => {
    const proposed = {
      ...this.state.preview.suggested
    };

    this.setState({ proposed });
    this.previewTags(proposed);
  };

  onPreviewPress = () => {
    this.previewTags(this.state.proposed);
  };

  onWritePress = () => {
    this.setState({ isSaving: true, saveError: null });

    const promise = createAjaxRequest({
      url: `/bookFile/${this.props.id}/audioTag`,
      method: 'PUT',
      dataType: 'json',
      data: JSON.stringify(this.state.proposed)
    }).request;

    promise.done((preview) => {
      this.setState({
        isSaving: false,
        saveError: null,
        preview,
        proposed: preview.proposed
      });
    });

    promise.fail((xhr) => {
      this.setState({
        isSaving: false,
        saveError: xhr
      });
    });
  };

  renderChanges() {
    const {
      preview
    } = this.state;

    if (!preview.changes.length) {
      return (
        <div className={styles.emptyChanges}>
          No tag changes in the current preview.
        </div>
      );
    }

    return (
      <table className={styles.changesTable}>
        <thead>
          <tr>
            <th>Field</th>
            <th>Current</th>
            <th>Proposed</th>
          </tr>
        </thead>
        <tbody>
          {
            preview.changes.map((change) => {
              return (
                <tr key={change.field}>
                  <td>{change.field}</td>
                  <td>{change.currentValue}</td>
                  <td>{change.proposedValue}</td>
                </tr>
              );
            })
          }
        </tbody>
      </table>
    );
  }

  render() {
    const {
      isFetching,
      isPreviewing,
      isSaving,
      error,
      saveError,
      preview,
      proposed
    } = this.state;

    if (isFetching) {
      return (
        <LoadingIndicator />
      );
    }

    if (error) {
      return (
        <Alert kind={kinds.WARNING}>
          {getErrorMessage(error, 'Unable to load audio tag editor')}
        </Alert>
      );
    }

    return (
      <div className={styles.editor}>
        <div className={styles.heading}>
          Manual Audio Tags
        </div>

        {
          preview.warning &&
            <Alert kind={kinds.WARNING}>
              {preview.warning}
            </Alert>
        }

        {
          saveError &&
            <Alert kind={kinds.DANGER}>
              {getErrorMessage(saveError, 'Unable to write audio tags')}
            </Alert>
        }

        {
          fields.map((field) => {
            return (
              <FormGroup key={field.name}>
                <FormLabel>{field.label}</FormLabel>
                <FormInputGroup
                  name={field.name}
                  type={field.type || inputTypes.TEXT}
                  value={proposed[field.name] ?? ''}
                  min={0}
                  isDisabled={isPreviewing || isSaving}
                  onChange={this.onInputChange}
                />
              </FormGroup>
            );
          })
        }

        <div className={styles.changes}>
          {this.renderChanges()}
        </div>

        <div className={styles.actions}>
          <Button
            onPress={this.onUseSuggestedPress}
            isDisabled={isPreviewing || isSaving}
          >
            Use ReadAIrr Tags
          </Button>

          <Button
            onPress={this.onPreviewPress}
            isDisabled={isPreviewing || isSaving}
          >
            Preview
          </Button>

          <Button
            kind={kinds.PRIMARY}
            onPress={this.onWritePress}
            isDisabled={isPreviewing || isSaving || !preview.canWrite || !preview.changes.length}
          >
            Write Tags
          </Button>
        </div>
      </div>
    );
  }
}

AudioTagEditor.propTypes = {
  id: PropTypes.number.isRequired
};

export default AudioTagEditor;
