import PropTypes from 'prop-types';
import React, { Component } from 'react';
import Alert from 'Components/Alert';
import FieldSet from 'Components/FieldSet';
import Form from 'Components/Form/Form';
import FormGroup from 'Components/Form/FormGroup';
import FormInputGroup from 'Components/Form/FormInputGroup';
import FormLabel from 'Components/Form/FormLabel';
import SpinnerButton from 'Components/Link/SpinnerButton';
import LoadingIndicator from 'Components/Loading/LoadingIndicator';
import PageContent from 'Components/Page/PageContent';
import PageContentBody from 'Components/Page/PageContentBody';
import { inputTypes, kinds } from 'Helpers/Props';
import SettingsToolbarConnector from 'Settings/SettingsToolbarConnector';
import translate from 'Utilities/String/translate';
import styles from './DevelopmentSettings.css';

const logLevelOptions = [
  { key: 'info', value: 'Info' },
  { key: 'debug', value: 'Debug' },
  { key: 'trace', value: 'Trace' }
];

const speechToTextProviderOptions = [
  { key: 'disabled', value: 'Disabled' },
  { key: 'openrouter', value: 'OpenRouter speech-to-text' },
  { key: 'openai-compatible', value: 'OpenAI-compatible speech-to-text' }
];

const matchingPresetOptions = [
  { key: 'relaxed', value: 'Relaxed - 70%', threshold: 70 },
  { key: 'balanced', value: 'Balanced - 80% default', threshold: 80 },
  { key: 'strict', value: 'Strict - 90%', threshold: 90 },
  { key: 'custom', value: 'Custom' }
];

const GOODREADS_METADATA_SOURCE = 'https://api.bookinfo.pro';
const HARDCOVER_METADATA_SOURCE = 'https://hardcover.bookinfo.pro';
const CUSTOM_METADATA_SOURCE_EXAMPLE = 'http://rreading-glasses:8788';
const CUSTOM_METADATA_SOURCE = 'custom';

const metadataSourceOptions = [
  { key: GOODREADS_METADATA_SOURCE, value: 'Goodreads hosted', hint: GOODREADS_METADATA_SOURCE },
  { key: HARDCOVER_METADATA_SOURCE, value: 'Hardcover hosted', hint: HARDCOVER_METADATA_SOURCE },
  { key: CUSTOM_METADATA_SOURCE, value: 'Custom rreading-glasses URL', hint: CUSTOM_METADATA_SOURCE_EXAMPLE }
];

function getMetadataSourceOption(metadataSource, metadataSourceMode) {
  if (metadataSourceMode === CUSTOM_METADATA_SOURCE) {
    return CUSTOM_METADATA_SOURCE;
  }

  const metadataSourceValue = metadataSource || GOODREADS_METADATA_SOURCE;
  const metadataSourceOption = metadataSourceOptions.find((option) => option.key === metadataSourceValue);

  return metadataSourceOption ? metadataSourceValue : CUSTOM_METADATA_SOURCE;
}

function getMatchingThreshold(settings) {
  const value = settings.minimumBookMatchSimilarity?.value;
  const threshold = parseInt(value);

  return isNaN(threshold) ? 80 : threshold;
}

function getMatchingPresetOption(threshold) {
  const preset = matchingPresetOptions.find((option) => option.threshold === threshold);

  return preset ? preset.key : 'custom';
}

function getMatchingCriteriaSummary(threshold) {
  const allowedDistance = 100 - threshold;

  return `Current matching minimum is ${threshold}%. Candidates below this confidence are rejected. This allows up to ${allowedDistance}% normalized title/author/edition distance and applies to automatic imports, manual import identification, retry identify, and unmapped triage.`;
}

class DevelopmentSettings extends Component {

  //
  // Lifecycle

  constructor(props, context) {
    super(props, context);

    this.state = {
      metadataSourceMode: null
    };
  }

  //
  // Listeners

  onMetadataSourceOptionChange = ({ value }) => {
    if (value === CUSTOM_METADATA_SOURCE) {
      this.setState({ metadataSourceMode: CUSTOM_METADATA_SOURCE });

      const currentMetadataSource = this.props.settings.metadataSource?.value;

      if (!currentMetadataSource || currentMetadataSource === GOODREADS_METADATA_SOURCE || currentMetadataSource === HARDCOVER_METADATA_SOURCE) {
        this.props.onInputChange({
          name: 'metadataSource',
          value: CUSTOM_METADATA_SOURCE_EXAMPLE
        });
      }

      return;
    }

    this.setState({ metadataSourceMode: null });

    this.props.onInputChange({
      name: 'metadataSource',
      value
    });
  };

  onCustomMetadataSourceChange = ({ value }) => {
    this.props.onInputChange({
      name: 'metadataSource',
      value
    });
  };

  onMatchingPresetChange = ({ value }) => {
    const preset = matchingPresetOptions.find((option) => option.key === value);

    if (!preset?.threshold) {
      return;
    }

    this.props.onInputChange({
      name: 'minimumBookMatchSimilarity',
      value: preset.threshold
    });
  };

  //
  // Render

  render() {
    const {
      isFetching,
      error,
      settings,
      hasSettings,
      onInputChange,
      onSavePress,
      onTestMetadataSourcePress,
      onTestOpenRouterPress,
      isTestingMetadataSource,
      metadataSourceTestResult,
      metadataSourceTestError,
      isTestingOpenRouter,
      openRouterTestResult,
      openRouterTestError,
      ...otherProps
    } = this.props;

    const metadataSourceSetting = settings.metadataSource || {};
    const metadataSource = metadataSourceSetting.value || GOODREADS_METADATA_SOURCE;
    const metadataSourceOption = getMetadataSourceOption(metadataSource, this.state.metadataSourceMode);
    const isCustomMetadataSource = metadataSourceOption === CUSTOM_METADATA_SOURCE;
    const matchingThreshold = getMatchingThreshold(settings);
    const matchingPresetOption = getMatchingPresetOption(matchingThreshold);

    return (
      <PageContent title={translate('Development')}>
        <SettingsToolbarConnector
          {...otherProps}
          onSavePress={onSavePress}
        />

        <PageContentBody>
          {
            isFetching &&
              <LoadingIndicator />
          }

          {
            !isFetching && error &&
              <div>
                Unable to load Development settings
              </div>
          }

          {
            hasSettings && !isFetching && !error &&
              <Form
                id="developmentSettings"
                {...otherProps}
              >
                <FieldSet legend={translate('MetadataProviderSource')}>
                  <FormGroup>
                    <FormLabel>
                      {translate('MetadataSource')}
                    </FormLabel>

                    <FormInputGroup
                      type={inputTypes.SELECT}
                      name="metadataSourceOption"
                      value={metadataSourceOption}
                      values={metadataSourceOptions}
                      helpText={translate('MetadataSourceHelpText')}
                      helpTextWarning={translate('MetadataSourceHelpTextWarning')}
                      onChange={this.onMetadataSourceOptionChange}
                    />
                  </FormGroup>

                  {
                    isCustomMetadataSource &&
                      <FormGroup>
                        <FormLabel>
                          {translate('CustomMetadataSource')}
                        </FormLabel>

                        <FormInputGroup
                          type={inputTypes.TEXT}
                          name="metadataSource"
                          helpText={translate('CustomMetadataSourceHelpText')}
                          helpLink="https://github.com/blampe/rreading-glasses#self-hosting"
                          onChange={this.onCustomMetadataSourceChange}
                          {...metadataSourceSetting}
                        />
                      </FormGroup>
                  }

                  <FormGroup>
                    <FormLabel>
                      {translate('Test')}
                    </FormLabel>

                    <div>
                      <SpinnerButton
                        kind={kinds.PRIMARY}
                        isSpinning={isTestingMetadataSource}
                        onPress={onTestMetadataSourcePress}
                      >
                        {translate('TestMetadataSource')}
                      </SpinnerButton>

                      {
                        metadataSourceTestResult &&
                          <Alert
                            className={styles.testResult}
                            kind={metadataSourceTestResult.isHealthy ? kinds.SUCCESS : kinds.DANGER}
                          >
                            <div>{metadataSourceTestResult.message}</div>

                            {
                              metadataSourceTestResult.detail &&
                                <div className={styles.testDetail}>{metadataSourceTestResult.detail}</div>
                            }

                            {
                              metadataSourceTestResult.statusCode &&
                                <div className={styles.testDetail}>
                                  HTTP {metadataSourceTestResult.statusCode}
                                </div>
                            }
                          </Alert>
                      }

                      {
                        metadataSourceTestError &&
                          <Alert
                            className={styles.testResult}
                            kind={kinds.DANGER}
                          >
                            {translate('MetadataSourceTestFailed')}
                          </Alert>
                      }
                    </div>
                  </FormGroup>
                </FieldSet>

                <FieldSet legend="Matching Criteria">
                  <FormGroup>
                    <FormLabel>
                      Preset
                    </FormLabel>

                    <FormInputGroup
                      type={inputTypes.SELECT}
                      name="minimumBookMatchSimilarityPreset"
                      value={matchingPresetOption}
                      values={matchingPresetOptions}
                      helpText="Choose a safe preset, or fine tune the exact percentage below."
                      onChange={this.onMatchingPresetChange}
                    />
                  </FormGroup>

                  <FormGroup>
                    <FormLabel>
                      {translate('MinimumBookMatchSimilarity')}
                    </FormLabel>

                    <FormInputGroup
                      type={inputTypes.NUMBER}
                      name="minimumBookMatchSimilarity"
                      min={50}
                      max={100}
                      helpText={translate('MinimumBookMatchSimilarityHelpText')}
                      onChange={onInputChange}
                      {...settings.minimumBookMatchSimilarity}
                    />
                  </FormGroup>

                  <Alert kind={kinds.INFO}>
                    {getMatchingCriteriaSummary(matchingThreshold)}
                  </Alert>
                </FieldSet>

                <FieldSet legend="AI / Deep Identification">
                  <FormGroup>
                    <FormLabel>
                      OpenRouter enabled
                    </FormLabel>

                    <FormInputGroup
                      type={inputTypes.CHECK}
                      name="openRouterEnabled"
                      helpText="AI Review and Deep Identify are opt-in manual actions for selected unmapped files. Normal matching does not require OpenRouter."
                      onChange={onInputChange}
                      {...settings.openRouterEnabled}
                    />
                  </FormGroup>

                  <FormGroup>
                    <FormLabel>
                      OpenRouter API key
                    </FormLabel>

                    <FormInputGroup
                      type={inputTypes.PASSWORD}
                      name="openRouterApiKey"
                      helpText="Stored server-side and redacted in API responses. The key is never sent back to the browser after save."
                      onChange={onInputChange}
                      {...settings.openRouterApiKey}
                    />
                  </FormGroup>

                  <FormGroup>
                    <FormLabel>
                      OpenRouter base URL
                    </FormLabel>

                    <FormInputGroup
                      type={inputTypes.TEXT}
                      name="openRouterBaseUrl"
                      helpText="Default: https://openrouter.ai/api/v1"
                      onChange={onInputChange}
                      {...settings.openRouterBaseUrl}
                    />
                  </FormGroup>

                  <FormGroup>
                    <FormLabel>
                      OpenRouter model
                    </FormLabel>

                    <FormInputGroup
                      type={inputTypes.TEXT}
                      name="openRouterModel"
                      helpText="Used for manual AI Review suggestions. Suggestions require user confirmation and never auto-import."
                      onChange={onInputChange}
                      {...settings.openRouterModel}
                    />
                  </FormGroup>

                  <FormGroup>
                    <FormLabel>
                      Timeout
                    </FormLabel>

                    <FormInputGroup
                      type={inputTypes.NUMBER}
                      name="openRouterTimeout"
                      min={5}
                      max={120}
                      helpText="Request timeout in seconds."
                      onChange={onInputChange}
                      {...settings.openRouterTimeout}
                    />
                  </FormGroup>

                  <FormGroup>
                    <FormLabel>
                      Max files per AI review
                    </FormLabel>

                    <FormInputGroup
                      type={inputTypes.NUMBER}
                      name="openRouterMaxFileContext"
                      min={1}
                      max={20}
                      helpText="Limits selected-file context sent to the provider."
                      onChange={onInputChange}
                      {...settings.openRouterMaxFileContext}
                    />
                  </FormGroup>

                  <FormGroup>
                    <FormLabel>
                      {translate('Test')}
                    </FormLabel>

                    <div>
                      <SpinnerButton
                        kind={kinds.PRIMARY}
                        isSpinning={isTestingOpenRouter}
                        onPress={onTestOpenRouterPress}
                      >
                        Test OpenRouter
                      </SpinnerButton>

                      {
                        openRouterTestResult &&
                          <Alert
                            className={styles.testResult}
                            kind={openRouterTestResult.isHealthy ? kinds.SUCCESS : kinds.WARNING}
                          >
                            <div>{openRouterTestResult.message}</div>

                            {
                              openRouterTestResult.detail &&
                                <div className={styles.testDetail}>{openRouterTestResult.detail}</div>
                            }

                            {
                              openRouterTestResult.statusCode &&
                                <div className={styles.testDetail}>
                                  HTTP {openRouterTestResult.statusCode}
                                </div>
                            }
                          </Alert>
                      }

                      {
                        openRouterTestError &&
                          <Alert
                            className={styles.testResult}
                            kind={kinds.DANGER}
                          >
                            OpenRouter test failed
                          </Alert>
                      }
                    </div>
                  </FormGroup>

                  <Alert kind={kinds.INFO}>
                    AI Review sends only selected file path/name, parsed metadata, candidate summary, confidence, and rejection reasons to your configured provider. Suggestions are review-only and never auto-import.
                  </Alert>
                </FieldSet>

                <FieldSet legend="Speech-to-Text">
                  <FormGroup>
                    <FormLabel>
                      Provider
                    </FormLabel>

                    <FormInputGroup
                      type={inputTypes.SELECT}
                      name="speechToTextProvider"
                      values={speechToTextProviderOptions}
                      helpText="Deep Identify Audio is manual-triggered only. Only the short intro segment is sent when a speech-to-text provider is configured."
                      onChange={onInputChange}
                      {...settings.speechToTextProvider}
                    />
                  </FormGroup>

                  {
                    settings.speechToTextProvider.value === 'openai-compatible' &&
                      <FormGroup>
                        <FormLabel>
                          Provider API key
                        </FormLabel>

                        <FormInputGroup
                          type={inputTypes.PASSWORD}
                          name="speechToTextApiKey"
                          helpText="Stored server-side and redacted in API responses. Only needed for the OpenAI-compatible fallback provider."
                          onChange={onInputChange}
                          {...settings.speechToTextApiKey}
                        />
                      </FormGroup>
                  }

                  {
                    settings.speechToTextProvider.value === 'openai-compatible' &&
                      <FormGroup>
                        <FormLabel>
                          Provider base URL
                        </FormLabel>

                        <FormInputGroup
                          type={inputTypes.TEXT}
                          name="speechToTextBaseUrl"
                          helpText="Optional OpenAI-compatible speech-to-text endpoint URL."
                          onChange={onInputChange}
                          {...settings.speechToTextBaseUrl}
                        />
                      </FormGroup>
                  }

                  <FormGroup>
                    <FormLabel>
                      Provider model
                    </FormLabel>

                    <FormInputGroup
                      type={inputTypes.TEXT}
                      name="speechToTextModel"
                      helpText="Provider-specific speech-to-text model name. OpenRouter default: openai/whisper-1."
                      onChange={onInputChange}
                      {...settings.speechToTextModel}
                    />
                  </FormGroup>

                  <FormGroup>
                    <FormLabel>
                      Intro window
                    </FormLabel>

                    <FormInputGroup
                      type={inputTypes.NUMBER}
                      name="speechToTextIntroSeconds"
                      min={5}
                      max={120}
                      helpText="Maximum beginning segment length, in seconds. Use about 60 seconds to capture title, author, narrator, and publisher intro clues without sending the full file."
                      onChange={onInputChange}
                      {...settings.speechToTextIntroSeconds}
                    />
                  </FormGroup>

                  <Alert kind={kinds.INFO}>
                    OpenRouter speech-to-text uses the existing OpenRouter API key. The OpenAI-compatible option is an advanced fallback with a separate key. Deep Identify Audio never transcribes automatically during scans.
                  </Alert>
                </FieldSet>

                <FieldSet legend={translate('Logging')}>
                  <FormGroup>
                    <FormLabel>
                      {translate('LogRotation')}
                    </FormLabel>

                    <FormInputGroup
                      type={inputTypes.NUMBER}
                      name="logRotate"
                      helpText={translate('LogRotateHelpText')}
                      onChange={onInputChange}
                      {...settings.logRotate}
                    />
                  </FormGroup>

                  <FormGroup>
                    <FormLabel>
                      {translate('ConsoleLogLevel')}
                    </FormLabel>
                    <FormInputGroup
                      type={inputTypes.SELECT}
                      name="consoleLogLevel"
                      values={logLevelOptions}
                      onChange={onInputChange}
                      {...settings.consoleLogLevel}
                    />
                  </FormGroup>

                  <FormGroup>
                    <FormLabel>
                      {translate('LogSQL')}
                    </FormLabel>

                    <FormInputGroup
                      type={inputTypes.CHECK}
                      name="logSql"
                      helpText={translate('LogSqlHelpText')}
                      onChange={onInputChange}
                      {...settings.logSql}
                    />
                  </FormGroup>
                </FieldSet>

                <FieldSet legend={translate('Analytics')}>
                  <FormGroup>
                    <FormLabel>
                      {translate('FilterAnalyticsEvents')}
                    </FormLabel>

                    <FormInputGroup
                      type={inputTypes.CHECK}
                      name="filterSentryEvents"
                      helpText={translate('FilterSentryEventsHelpText')}
                      onChange={onInputChange}
                      {...settings.filterSentryEvents}
                    />
                  </FormGroup>
                </FieldSet>
              </Form>
          }
        </PageContentBody>
      </PageContent>
    );
  }

}

DevelopmentSettings.propTypes = {
  isFetching: PropTypes.bool.isRequired,
  error: PropTypes.object,
  settings: PropTypes.object.isRequired,
  hasSettings: PropTypes.bool.isRequired,
  onSavePress: PropTypes.func.isRequired,
  onInputChange: PropTypes.func.isRequired,
  onTestMetadataSourcePress: PropTypes.func.isRequired,
  onTestOpenRouterPress: PropTypes.func.isRequired,
  isTestingMetadataSource: PropTypes.bool.isRequired,
  metadataSourceTestResult: PropTypes.object,
  metadataSourceTestError: PropTypes.object,
  isTestingOpenRouter: PropTypes.bool.isRequired,
  openRouterTestResult: PropTypes.object,
  openRouterTestError: PropTypes.object
};

export default DevelopmentSettings;
