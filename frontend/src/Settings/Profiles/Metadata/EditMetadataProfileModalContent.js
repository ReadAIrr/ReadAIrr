import PropTypes from 'prop-types';
import React from 'react';
import Form from 'Components/Form/Form';
import FormGroup from 'Components/Form/FormGroup';
import FormInputGroup from 'Components/Form/FormInputGroup';
import FormLabel from 'Components/Form/FormLabel';
import Button from 'Components/Link/Button';
import SpinnerErrorButton from 'Components/Link/SpinnerErrorButton';
import LoadingIndicator from 'Components/Loading/LoadingIndicator';
import ModalBody from 'Components/Modal/ModalBody';
import ModalContent from 'Components/Modal/ModalContent';
import ModalFooter from 'Components/Modal/ModalFooter';
import ModalHeader from 'Components/Modal/ModalHeader';
import { inputTypes, kinds } from 'Helpers/Props';
import translate from 'Utilities/String/translate';
import styles from './EditMetadataProfileModalContent.css';

function EditMetadataProfileModalContent(props) {
  const {
    isFetching,
    error,
    isSaving,
    saveError,
    item,
    isInUse,
    onInputChange,
    onSavePress,
    onModalClose,
    onDeleteMetadataProfilePress,
    isPreviewFetching,
    previewError,
    preview,
    ...otherProps
  } = props;

  const {
    id,
    name,
    minPopularity,
    skipMissingDate,
    skipMissingIsbn,
    skipPartsAndSets,
    skipSeriesSecondary,
    requireReadable,
    requireAudio,
    skipAnthologies,
    skipCollections,
    skipSerializedParts,
    skipEssays,
    skipShortStories,
    allowedLanguages,
    ignored,
    minPages
  } = item;

  return (
    <ModalContent onModalClose={onModalClose}>
      <ModalHeader>
        {id ? 'Edit Metadata Profile' : 'Add Metadata Profile'}
      </ModalHeader>

      <ModalBody>
        {
          isFetching &&
            <LoadingIndicator />
        }

        {
          !isFetching && !!error &&
            <div>
              {translate('UnableToAddANewMetadataProfilePleaseTryAgain')}
            </div>
        }

        {
          !isFetching && !error &&
            <Form {...otherProps}>
              <FormGroup>
                <FormLabel>
                  {translate('Name')}
                </FormLabel>

                <FormInputGroup
                  type={inputTypes.TEXT}
                  name="name"
                  {...name}
                  onChange={onInputChange}
                />
              </FormGroup>

              <FormGroup>
                <FormLabel>
                  {translate('MinimumPopularity')}
                </FormLabel>

                <FormInputGroup
                  type={inputTypes.NUMBER}
                  name="minPopularity"
                  {...minPopularity}
                  helpText={translate('MinPopularityHelpText')}
                  isFloat={true}
                  min={0}
                  onChange={onInputChange}
                />
              </FormGroup>

              <FormGroup>
                <FormLabel>
                  {translate('MinimumPages')}
                </FormLabel>

                <FormInputGroup
                  type={inputTypes.NUMBER}
                  name="minPages"
                  {...minPages}
                  helpText={translate('MinPagesHelpText')}
                  min={0}
                  onChange={onInputChange}
                />
              </FormGroup>

              <FormGroup>
                <FormLabel>
                  {translate('SkipBooksWithMissingReleaseDate')}
                </FormLabel>

                <FormInputGroup
                  type={inputTypes.CHECK}
                  name="skipMissingDate"
                  {...skipMissingDate}
                  onChange={onInputChange}
                />
              </FormGroup>

              <FormGroup>
                <FormLabel>
                  {translate('SkipBooksWithNoISBNOrASIN')}
                </FormLabel>

                <FormInputGroup
                  type={inputTypes.CHECK}
                  name="skipMissingIsbn"
                  {...skipMissingIsbn}
                  onChange={onInputChange}
                />
              </FormGroup>

              <FormGroup>
                <FormLabel>
                  {translate('SkipPartBooksAndSets')}
                </FormLabel>

                <FormInputGroup
                  type={inputTypes.CHECK}
                  name="skipPartsAndSets"
                  {...skipPartsAndSets}
                  onChange={onInputChange}
                />
              </FormGroup>

              <FormGroup>
                <FormLabel>
                  {translate('SkipSecondarySeriesBooks')}
                </FormLabel>

                <FormInputGroup
                  type={inputTypes.CHECK}
                  name="skipSeriesSecondary"
                  {...skipSeriesSecondary}
                  onChange={onInputChange}
                />
              </FormGroup>

              <FormGroup>
                <FormLabel>
                  Readable/e-reader compatible
                </FormLabel>

                <FormInputGroup
                  type={inputTypes.CHECK}
                  name="requireReadable"
                  {...requireReadable}
                  helpText="Require editions marked as e-book/digital formats and exclude audio-only formats."
                  onChange={onInputChange}
                />
              </FormGroup>

              <FormGroup>
                <FormLabel>
                  Audio-compatible only
                </FormLabel>

                <FormInputGroup
                  type={inputTypes.CHECK}
                  name="requireAudio"
                  {...requireAudio}
                  helpText="Require audiobook/audio format metadata such as audiobook, audio CD, MP3, M4B, Audible, abridged, or unabridged."
                  onChange={onInputChange}
                />
              </FormGroup>

              <FormGroup>
                <FormLabel>
                  Exclude anthologies
                </FormLabel>

                <FormInputGroup
                  type={inputTypes.CHECK}
                  name="skipAnthologies"
                  {...skipAnthologies}
                  helpText="Exclude books with anthology metadata or title terms."
                  onChange={onInputChange}
                />
              </FormGroup>

              <FormGroup>
                <FormLabel>
                  Exclude collections and box sets
                </FormLabel>

                <FormInputGroup
                  type={inputTypes.CHECK}
                  name="skipCollections"
                  {...skipCollections}
                  helpText="Exclude omnibus, collection, collected, complete works, and box set terms."
                  onChange={onInputChange}
                />
              </FormGroup>

              <FormGroup>
                <FormLabel>
                  Exclude serialized parts
                </FormLabel>

                <FormInputGroup
                  type={inputTypes.CHECK}
                  name="skipSerializedParts"
                  {...skipSerializedParts}
                  helpText="Exclude books that look like serialized parts, episodes, or multi-work parts."
                  onChange={onInputChange}
                />
              </FormGroup>

              <FormGroup>
                <FormLabel>
                  Exclude essays
                </FormLabel>

                <FormInputGroup
                  type={inputTypes.CHECK}
                  name="skipEssays"
                  {...skipEssays}
                  helpText="Exclude books with essay metadata or title terms."
                  onChange={onInputChange}
                />
              </FormGroup>

              <FormGroup>
                <FormLabel>
                  Exclude short stories
                </FormLabel>

                <FormInputGroup
                  type={inputTypes.CHECK}
                  name="skipShortStories"
                  {...skipShortStories}
                  helpText="Exclude books with short story metadata or title terms."
                  onChange={onInputChange}
                />
              </FormGroup>

              <FormGroup>
                <FormLabel>
                  {translate('AllowedLanguages')}
                </FormLabel>

                <FormInputGroup
                  type={inputTypes.TEXT}
                  name="allowedLanguages"
                  {...allowedLanguages}
                  helpText={translate('Iso639-3')}
                  onChange={onInputChange}
                />
              </FormGroup>

              <FormGroup>
                <FormLabel>
                  {translate('MustNotContain')}
                </FormLabel>

                <FormInputGroup
                  type={inputTypes.TEXT_TAG}
                  name="ignored"
                  helpText={translate('IgnoredMetaHelpText')}
                  kind={kinds.DANGER}
                  placeholder={translate('IgnoredPlaceHolder')}
                  delimiters={['Tab', 'Enter', ',']}
                  {...ignored}
                  onChange={onInputChange}
                />
              </FormGroup>

              <div className={styles.preview}>
                <div className={styles.previewHeader}>
                  <div>
                    <div className={styles.previewTitle}>
                      Metadata profile preview
                    </div>
                    {
                      preview &&
                        <div className={styles.previewSummary}>
                          Evaluated {preview.evaluatedBooks} books and {preview.evaluatedEditions} editions in this library.
                        </div>
                    }
                  </div>

                  {
                    isPreviewFetching &&
                      <div className={styles.previewLoading}>
                        Updating...
                      </div>
                  }
                </div>

                {
                  previewError &&
                    <div className={styles.previewError}>
                      Unable to load metadata profile preview.
                    </div>
                }

                {
                  preview && !previewError &&
                    <div className={styles.previewRules}>
                      {
                        preview.rules.map((rule) => {
                          return (
                            <div
                              key={rule.key}
                              className={styles.previewRule}
                            >
                              <div className={styles.previewRuleHeader}>
                                <span>{rule.label}</span>
                                <span>{rule.count}</span>
                              </div>
                              <div className={styles.previewRuleDescription}>
                                {rule.description}
                              </div>
                              {
                                !!rule.examples.length &&
                                  <ul className={styles.previewExamples}>
                                    {
                                      rule.examples.map((example, index) => {
                                        return (
                                          <li key={index}>
                                            <span>{example.title}</span>
                                            <span>{example.detail}</span>
                                          </li>
                                        );
                                      })
                                    }
                                  </ul>
                              }
                            </div>
                          );
                        })
                      }
                    </div>
                }
              </div>
            </Form>
        }
      </ModalBody>
      <ModalFooter>
        {
          id &&
            <div
              className={styles.deleteButtonContainer}
              title={isInUse ? translate('IsInUseCantDeleteAMetadataProfileThatIsAttachedToAnAuthorOrImportList') : undefined}
            >
              <Button
                kind={kinds.DANGER}
                isDisabled={isInUse}
                onPress={onDeleteMetadataProfilePress}
              >
                Delete
              </Button>
            </div>
        }

        <Button
          onPress={onModalClose}
        >
          Cancel
        </Button>

        <SpinnerErrorButton
          isSpinning={isSaving}
          error={saveError}
          onPress={onSavePress}
        >
          Save
        </SpinnerErrorButton>
      </ModalFooter>
    </ModalContent>
  );
}

EditMetadataProfileModalContent.propTypes = {
  isFetching: PropTypes.bool.isRequired,
  error: PropTypes.object,
  isSaving: PropTypes.bool.isRequired,
  saveError: PropTypes.object,
  item: PropTypes.object.isRequired,
  isInUse: PropTypes.bool.isRequired,
  isPreviewFetching: PropTypes.bool.isRequired,
  previewError: PropTypes.object,
  preview: PropTypes.object,
  onInputChange: PropTypes.func.isRequired,
  onSavePress: PropTypes.func.isRequired,
  onModalClose: PropTypes.func.isRequired,
  onDeleteMetadataProfilePress: PropTypes.func
};

export default EditMetadataProfileModalContent;
