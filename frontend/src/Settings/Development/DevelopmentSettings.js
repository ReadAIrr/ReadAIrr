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

const GOODREADS_METADATA_SOURCE = 'https://api.bookinfo.pro';
const HARDCOVER_METADATA_SOURCE = 'https://hardcover.bookinfo.pro';
const LOCAL_METADATA_SOURCE = 'http://rreading-glasses:8788';
const ORIGINAL_METADATA_SOURCE = 'readarr://metadata/original';
const CUSTOM_METADATA_SOURCE = 'custom';

const metadataSourceOptions = [
  { key: LOCAL_METADATA_SOURCE, value: 'Automatic self-hosted rreading-glasses', hint: LOCAL_METADATA_SOURCE },
  { key: GOODREADS_METADATA_SOURCE, value: 'rreading-glasses (Goodreads hosted)', hint: GOODREADS_METADATA_SOURCE },
  { key: HARDCOVER_METADATA_SOURCE, value: 'rreading-glasses (Hardcover hosted)', hint: HARDCOVER_METADATA_SOURCE },
  { key: ORIGINAL_METADATA_SOURCE, value: 'Original Readarr metadata', hint: 'Legacy built-in source' },
  { key: CUSTOM_METADATA_SOURCE, value: 'Custom/self-hosted URL' }
];

function getMetadataSourceOption(metadataSource, metadataSourceMode) {
  if (metadataSourceMode === CUSTOM_METADATA_SOURCE) {
    return CUSTOM_METADATA_SOURCE;
  }

  const metadataSourceValue = metadataSource || LOCAL_METADATA_SOURCE;
  const metadataSourceOption = metadataSourceOptions.find((option) => option.key === metadataSourceValue);

  return metadataSourceOption ? metadataSourceValue : CUSTOM_METADATA_SOURCE;
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
      isTestingMetadataSource,
      metadataSourceTestResult,
      metadataSourceTestError,
      ...otherProps
    } = this.props;

    const metadataSourceSetting = settings.metadataSource || {};
    const metadataSource = metadataSourceSetting.value || LOCAL_METADATA_SOURCE;
    const metadataSourceOption = getMetadataSourceOption(metadataSource, this.state.metadataSourceMode);
    const isCustomMetadataSource = metadataSourceOption === CUSTOM_METADATA_SOURCE;

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
                      helpLink="https://github.com/blampe/rreading-glasses#usage"
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

                <FieldSet legend={translate('Matching')}>
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
  isTestingMetadataSource: PropTypes.bool.isRequired,
  metadataSourceTestResult: PropTypes.object,
  metadataSourceTestError: PropTypes.object
};

export default DevelopmentSettings;
