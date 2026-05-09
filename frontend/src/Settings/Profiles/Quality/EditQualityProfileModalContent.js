import PropTypes from 'prop-types';
import React, { Component } from 'react';
import Form from 'Components/Form/Form';
import FormGroup from 'Components/Form/FormGroup';
import FormInputGroup from 'Components/Form/FormInputGroup';
import FormLabel from 'Components/Form/FormLabel';
import Button from 'Components/Link/Button';
import SpinnerErrorButton from 'Components/Link/SpinnerErrorButton';
import LoadingIndicator from 'Components/Loading/LoadingIndicator';
import Measure from 'Components/Measure';
import ModalBody from 'Components/Modal/ModalBody';
import ModalContent from 'Components/Modal/ModalContent';
import ModalFooter from 'Components/Modal/ModalFooter';
import ModalHeader from 'Components/Modal/ModalHeader';
import { inputTypes, kinds, sizes } from 'Helpers/Props';
import dimensions from 'Styles/Variables/dimensions';
import translate from 'Utilities/String/translate';
import QualityProfileFormatItems from './QualityProfileFormatItems';
import QualityProfileItems from './QualityProfileItems';
import styles from './EditQualityProfileModalContent.css';

const MODAL_BODY_PADDING = parseInt(dimensions.modalBodyPadding);

const audiobookLayoutPreferenceOptions = [
  { key: 0, value: 'No layout preference' },
  { key: 1, value: 'Prefer single-file audiobooks' },
  { key: 2, value: 'Prefer multi-file audiobooks' }
];

const audiobookFormatPreferenceOptions = [
  { key: 0, value: 'No format preference' },
  { key: 1, value: 'Prefer M4B' },
  { key: 2, value: 'Prefer MP3' }
];

const audiobookFileCountPreferenceOptions = [
  { key: 0, value: 'No file-count preference' },
  { key: 1, value: 'Prefer fewer parts' }
];

const audiobookShapePreferenceOptions = [
  { key: 'multiFileMp3', value: 'Multi-file MP3' },
  { key: 'multiFileM4B', value: 'Multi-file M4B' },
  { key: 'singleFileMp3', value: 'Single-file MP3' },
  { key: 'singleFileM4B', value: 'Single-file M4B' }
];

function getCustomFormatRender(formatItems, otherProps) {
  return (
    <QualityProfileFormatItems
      profileFormatItems={formatItems.value}
      errors={formatItems.errors}
      warnings={formatItems.warnings}
      {...otherProps}
    />
  );
}

class EditQualityProfileModalContent extends Component {

  //
  // Lifecycle

  constructor(props, context) {
    super(props, context);

    this.state = {
      headerHeight: 0,
      bodyHeight: 0,
      footerHeight: 0
    };
  }

  componentDidUpdate(prevProps, prevState) {
    const {
      headerHeight,
      bodyHeight,
      footerHeight
    } = this.state;

    if (
      headerHeight > 0 &&
      bodyHeight > 0 &&
      footerHeight > 0 &&
      (
        headerHeight !== prevState.headerHeight ||
        bodyHeight !== prevState.bodyHeight ||
        footerHeight !== prevState.footerHeight
      )
    ) {
      const padding = MODAL_BODY_PADDING * 2;

      this.props.onContentHeightChange(
        headerHeight + bodyHeight + footerHeight + padding
      );
    }
  }

  //
  // Listeners

  onHeaderMeasure = ({ height }) => {
    if (height > this.state.headerHeight) {
      this.setState({ headerHeight: height });
    }
  };

  onBodyMeasure = ({ height }) => {

    if (height > this.state.bodyHeight) {
      this.setState({ bodyHeight: height });
    }
  };

  onFooterMeasure = ({ height }) => {
    if (height > this.state.footerHeight) {
      this.setState({ footerHeight: height });
    }
  };

  //
  // Render

  render() {
    const {
      editGroups,
      isFetching,
      error,
      isSaving,
      saveError,
      qualities,
      customFormats,
      item,
      isInUse,
      onInputChange,
      onAudiobookShapePreferenceOrderEnabledChange,
      onAudiobookShapePreferenceOrderChange,
      onCutoffChange,
      onSavePress,
      onModalClose,
      onDeleteQualityProfilePress,
      ...otherProps
    } = this.props;

    const {
      id,
      name,
      upgradeAllowed,
      cutoff,
      minFormatScore,
      cutoffFormatScore,
      audiobookLayoutPreference = { value: 0 },
      audiobookFormatPreference = { value: 0 },
      audiobookFileCountPreference = { value: 0 },
      audiobookShapePreferenceOrder = { value: [] },
      items,
      formatItems
    } = item;
    const audiobookShapePreferenceOrderValue = audiobookShapePreferenceOrder.value || [];
    const isCustomAudiobookShapeOrderEnabled = audiobookShapePreferenceOrderValue.length > 0;

    return (
      <ModalContent onModalClose={onModalClose}>
        <Measure
          onMeasure={this.onHeaderMeasure}
        >
          <ModalHeader>
            {id ? 'Edit Quality Profile' : 'Add Quality Profile'}
          </ModalHeader>
        </Measure>

        <ModalBody>
          <Measure
            onMeasure={this.onBodyMeasure}
          >
            {
              isFetching &&
                <LoadingIndicator />
            }

            {
              !isFetching && !!error &&
                <div>
                  {translate('UnableToAddANewQualityProfilePleaseTryAgain')}
                </div>
            }

            {
              !isFetching && !error &&
                <Form {...otherProps}>
                  <div className={styles.formGroupsContainer}>
                    <div className={styles.formGroupWrapper}>
                      <FormGroup size={sizes.EXTRA_SMALL}>
                        <FormLabel size={sizes.SMALL}>
                          Name
                        </FormLabel>

                        <FormInputGroup
                          type={inputTypes.TEXT}
                          name="name"
                          {...name}
                          onChange={onInputChange}
                        />
                      </FormGroup>

                      <FormGroup size={sizes.EXTRA_SMALL}>
                        <FormLabel size={sizes.SMALL}>
                          {translate('UpgradesAllowed')}
                        </FormLabel>

                        <FormInputGroup
                          type={inputTypes.CHECK}
                          name="upgradeAllowed"
                          {...upgradeAllowed}
                          helpText={translate('UpgradeAllowedHelpText')}
                          onChange={onInputChange}
                        />
                      </FormGroup>

                      {
                        upgradeAllowed.value &&
                          <FormGroup size={sizes.EXTRA_SMALL}>
                            <FormLabel size={sizes.SMALL}>
                              Upgrade Until
                            </FormLabel>

                            <FormInputGroup
                              type={inputTypes.SELECT}
                              name="cutoff"
                              {...cutoff}
                              values={qualities}
                              helpText={translate('CutoffHelpText')}
                              onChange={onCutoffChange}
                            />
                          </FormGroup>
                      }

                      {
                        formatItems.value.length > 0 &&
                          <FormGroup size={sizes.EXTRA_SMALL}>
                            <FormLabel size={sizes.SMALL}>
                              Minimum Custom Format Score
                            </FormLabel>

                            <FormInputGroup
                              type={inputTypes.NUMBER}
                              name="minFormatScore"
                              {...minFormatScore}
                              helpText={translate('MinFormatScoreHelpText')}
                              onChange={onInputChange}
                            />
                          </FormGroup>
                      }

                      {
                        upgradeAllowed.value && formatItems.value.length > 0 &&
                          <FormGroup size={sizes.EXTRA_SMALL}>
                            <FormLabel size={sizes.SMALL}>
                              Upgrade Until Custom Format Score
                            </FormLabel>

                            <FormInputGroup
                              type={inputTypes.NUMBER}
                              name="cutoffFormatScore"
                              {...cutoffFormatScore}
                              helpText={translate('CutoffFormatScoreHelpText')}
                              onChange={onInputChange}
                            />
                          </FormGroup>
                      }

                      <FormGroup size={sizes.EXTRA_SMALL}>
                        <FormLabel size={sizes.SMALL}>
                          Audiobook Layout
                        </FormLabel>

                        <FormInputGroup
                          type={inputTypes.SELECT}
                          name="audiobookLayoutPreference"
                          {...audiobookLayoutPreference}
                          values={audiobookLayoutPreferenceOptions}
                          helpText="Optional audiobook upgrade preference. Unknown release layout stays neutral."
                          onChange={onInputChange}
                        />
                      </FormGroup>

                      <FormGroup size={sizes.EXTRA_SMALL}>
                        <FormLabel size={sizes.SMALL}>
                          Audiobook Format
                        </FormLabel>

                        <FormInputGroup
                          type={inputTypes.SELECT}
                          name="audiobookFormatPreference"
                          {...audiobookFormatPreference}
                          values={audiobookFormatPreferenceOptions}
                          helpText="Optional M4B/MP3 preference for audiobook releases where format can be inferred."
                          onChange={onInputChange}
                        />
                      </FormGroup>

                      <FormGroup size={sizes.EXTRA_SMALL}>
                        <FormLabel size={sizes.SMALL}>
                          Audiobook Shape Ranking
                        </FormLabel>

                        <FormInputGroup
                          type={inputTypes.CHECK}
                          name="audiobookShapePreferenceOrderEnabled"
                          value={isCustomAudiobookShapeOrderEnabled}
                          helpText="Optionally rank combined audiobook shapes for equal-quality and equal-custom-format upgrade tie-breaks. Unknown shapes stay neutral."
                          onChange={onAudiobookShapePreferenceOrderEnabledChange}
                        />
                      </FormGroup>

                      {
                        isCustomAudiobookShapeOrderEnabled &&
                          audiobookShapePreferenceOrderValue.map((shape, index) => {
                            return (
                              <FormGroup
                                key={index}
                                size={sizes.EXTRA_SMALL}
                              >
                                <FormLabel size={sizes.SMALL}>
                                  {`Shape Rank ${index + 1}`}
                                </FormLabel>

                                <FormInputGroup
                                  type={inputTypes.SELECT}
                                  name={`audiobookShapePreferenceOrder.${index}`}
                                  value={shape}
                                  values={audiobookShapePreferenceOptions}
                                  onChange={({ value }) => onAudiobookShapePreferenceOrderChange(index, value)}
                                />
                              </FormGroup>
                            );
                          })
                      }

                      <FormGroup size={sizes.EXTRA_SMALL}>
                        <FormLabel size={sizes.SMALL}>
                          Audiobook File Count
                        </FormLabel>

                        <FormInputGroup
                          type={inputTypes.SELECT}
                          name="audiobookFileCountPreference"
                          {...audiobookFileCountPreference}
                          values={audiobookFileCountPreferenceOptions}
                          helpText="Optionally prefer fewer audiobook parts when file-count data is known."
                          onChange={onInputChange}
                        />
                      </FormGroup>

                      <div className={styles.formatItemLarge}>
                        {getCustomFormatRender(formatItems, otherProps)}
                      </div>
                    </div>

                    <div className={styles.formGroupWrapper}>
                      <QualityProfileItems
                        editGroups={editGroups}
                        qualityProfileItems={items.value}
                        errors={items.errors}
                        warnings={items.warnings}
                        {...otherProps}
                      />
                    </div>

                    <div className={styles.formatItemSmall}>
                      {getCustomFormatRender(formatItems, otherProps)}
                    </div>
                  </div>
                </Form>
            }
          </Measure>
        </ModalBody>

        <Measure
          onMeasure={this.onFooterMeasure}
        >
          <ModalFooter>
            {
              id ?
                <div
                  className={styles.deleteButtonContainer}
                  title={isInUse ? translate('IsInUseCantDeleteAQualityProfileThatIsAttachedToAnAuthorOrImportList') : undefined}
                >
                  <Button
                    kind={kinds.DANGER}
                    isDisabled={isInUse}
                    onPress={onDeleteQualityProfilePress}
                  >
                    Delete
                  </Button>
                </div> :
                null
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
        </Measure>
      </ModalContent>
    );
  }
}

EditQualityProfileModalContent.propTypes = {
  editGroups: PropTypes.bool.isRequired,
  isFetching: PropTypes.bool.isRequired,
  error: PropTypes.object,
  isSaving: PropTypes.bool.isRequired,
  saveError: PropTypes.object,
  qualities: PropTypes.arrayOf(PropTypes.object).isRequired,
  customFormats: PropTypes.arrayOf(PropTypes.object).isRequired,
  item: PropTypes.object.isRequired,
  isInUse: PropTypes.bool.isRequired,
  onInputChange: PropTypes.func.isRequired,
  onAudiobookShapePreferenceOrderEnabledChange: PropTypes.func.isRequired,
  onAudiobookShapePreferenceOrderChange: PropTypes.func.isRequired,
  onCutoffChange: PropTypes.func.isRequired,
  onSavePress: PropTypes.func.isRequired,
  onContentHeightChange: PropTypes.func.isRequired,
  onModalClose: PropTypes.func.isRequired,
  onDeleteQualityProfilePress: PropTypes.func
};

export default EditQualityProfileModalContent;
