import React, { Component } from 'react';
import PropTypes from 'prop-types';
import { inputTypes, kinds, sizes } from 'Helpers/Props';
import Card from 'Components/Card';
import FieldSet from 'Components/FieldSet';
import FormGroup from 'Components/Form/FormGroup';
import FormInputGroup from 'Components/Form/FormInputGroup';
import FormLabel from 'Components/Form/FormLabel';
import Button from 'Components/Link/Button';
import LoadingIndicator from 'Components/Loading/LoadingIndicator';
import Alert from 'Components/Alert';
import Icon from 'Components/Icon';
import { icons } from 'Helpers/Props';
import GoogleBooksSetupWizard from './GoogleBooksSetupWizard';
import styles from './GoogleBooks.css';

class GoogleBooks extends Component {

  //
  // Lifecycle

  constructor(props, context) {
    super(props, context);

    this.state = {
      isSetupWizardOpen: false,
      config: null,
      isLoading: true,
      isTesting: false,
      testResult: null,
      hasChanges: false,
      error: null
    };
  }

  componentDidMount() {
    this._loadConfiguration();
  }

  //
  // Private Methods

  _loadConfiguration = async () => {
    this.setState({ isLoading: true, error: null });
    
    try {
      const response = await fetch('/api/v1/config/googlebooks/1');
      
      if (response.ok) {
        const config = await response.json();
        this.setState({
          config,
          isLoading: false
        });
      } else if (response.status === 404) {
        // No configuration exists yet
        this.setState({
          config: {
            id: 1,
            enabled: false,
            apiKey: ''
          },
          isLoading: false
        });
      } else {
        throw new Error(`HTTP ${response.status}`);
      }
    } catch (error) {
      this.setState({
        isLoading: false,
        error: 'Failed to load Google Books configuration'
      });
    }
  }

  _saveConfiguration = async () => {
    const { config } = this.state;
    
    try {
      const response = await fetch('/api/v1/config/googlebooks/1', {
        method: 'PUT',
        headers: {
          'Content-Type': 'application/json'
        },
        body: JSON.stringify(config)
      });

      if (!response.ok) {
        throw new Error(`HTTP ${response.status}`);
      }

      this.setState({ hasChanges: false });
    } catch (error) {
      this.setState({
        error: 'Failed to save configuration'
      });
    }
  }

  _testConnection = async () => {
    const { config } = this.state;
    
    if (!config?.apiKey?.trim()) {
      this.setState({
        testResult: {
          isValid: false,
          error: 'API key is required'
        }
      });
      return;
    }

    // Client-side validation
    const trimmedKey = config.apiKey.trim();
    if (trimmedKey.length < 30) {
      this.setState({
        testResult: {
          isValid: false,
          error: 'API key appears to be too short. Google API keys are typically 39 characters long.'
        }
      });
      return;
    }

    if (!trimmedKey.startsWith('AIza')) {
      this.setState({
        testResult: {
          isValid: false,
          error: 'API key format appears incorrect. Google Books API keys should start with "AIza".'
        }
      });
      return;
    }

    this.setState({
      isTesting: true,
      testResult: null,
      error: null
    });

    try {
      const controller = new AbortController();
      const timeoutId = setTimeout(() => controller.abort(), 30000); // 30 second timeout

      const response = await fetch('/api/v1/config/googlebooks/validate', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json'
        },
        body: JSON.stringify({ apiKey: trimmedKey }),
        signal: controller.signal
      });

      clearTimeout(timeoutId);

      if (!response.ok) {
        const errorData = await response.json().catch(() => ({ error: 'Unknown error' }));
        throw new Error(errorData.error || `HTTP ${response.status}: ${response.statusText}`);
      }

      const result = await response.json();
      
      this.setState({
        isTesting: false,
        testResult: result
      });
    } catch (error) {
      this.setState({
        isTesting: false,
        testResult: {
          isValid: false,
          error: error.name === 'AbortError' 
            ? 'Request timed out. Please check your internet connection and try again.'
            : error.message.includes('NetworkError') || error.message.includes('fetch')
            ? 'Network error. Please check your internet connection and try again.'
            : `Connection test failed: ${error.message}`
        }
      });
    }
  }

  //
  // Event Handlers

  onEnabledChange = ({ value }) => {
    this.setState({
      config: {
        ...this.state.config,
        enabled: value
      },
      hasChanges: true
    });
  }

  onApiKeyChange = ({ value }) => {
    this.setState({
      config: {
        ...this.state.config,
        apiKey: value
      },
      hasChanges: true,
      testResult: null
    });
  }

  onSavePress = () => {
    this._saveConfiguration();
  }

  onTestPress = () => {
    this._testConnection();
  }

  onSetupWizardOpen = () => {
    this.setState({ isSetupWizardOpen: true });
  }

  onSetupWizardClose = () => {
    this.setState({ isSetupWizardOpen: false });
  }

  onSetupWizardCompleted = () => {
    // Reload configuration after wizard completion
    this._loadConfiguration();
  }

  //
  // Render

  render() {
    const {
      isSetupWizardOpen,
      config,
      isLoading,
      isTesting,
      testResult,
      hasChanges,
      error
    } = this.state;

    if (isLoading) {
      return (
        <FieldSet legend="Google Books API">
          <div className={styles.loadingContainer}>
            <LoadingIndicator />
          </div>
        </FieldSet>
      );
    }

    const hasApiKey = config?.apiKey?.trim();
    const isConfigured = hasApiKey && config?.enabled;

    return (
      <FieldSet legend="Google Books API">
        <div className={styles.googleBooksContainer}>
          {error && (
            <Alert kind={kinds.DANGER}>
              {error}
            </Alert>
          )}

          <Card className={styles.infoCard}>
            <div className={styles.cardContent}>
              <div className={styles.iconSection}>
                <Icon name={icons.GOOGLE} size={32} />
              </div>
              <div className={styles.textSection}>
                <h4>Audiobook Duration Validation</h4>
                <p>
                  Connect to Google Books API to get audiobook duration data for validation.
                  This helps ensure your audiobook collection is complete by comparing file 
                  durations with expected runtime.
                </p>
              </div>
            </div>
          </Card>

          {!isConfigured && (
            <div className={styles.setupSection}>
              <Alert kind={kinds.INFO}>
                <strong>Setup Required:</strong> Configure Google Books API to enable 
                audiobook duration validation and enhanced metadata.
              </Alert>
              
              <div className={styles.setupButtonContainer}>
                <Button
                  kind={kinds.PRIMARY}
                  size={sizes.LARGE}
                  onPress={this.onSetupWizardOpen}
                >
                  <Icon name={icons.SETTINGS} />
                  Setup Google Books API
                </Button>
              </div>
            </div>
          )}

          {isConfigured && (
            <div className={styles.statusSection}>
              <Alert kind={kinds.SUCCESS}>
                <Icon name={icons.CHECK} />
                <strong>Configured:</strong> Google Books API is active and ready to use.
              </Alert>
            </div>
          )}

          <div className={styles.configSection}>
            <FormGroup>
              <FormLabel>Enable Google Books Integration</FormLabel>
              <FormInputGroup
                type={inputTypes.CHECK}
                name="enabled"
                value={config?.enabled || false}
                onChange={this.onEnabledChange}
                helpText="Enable Google Books API integration for enhanced metadata"
              />
            </FormGroup>

            <FormGroup>
              <FormLabel>API Key</FormLabel>
              <FormInputGroup
                type={inputTypes.PASSWORD}
                name="apiKey"
                value={config?.apiKey || ''}
                onChange={this.onApiKeyChange}
                placeholder="AIzaSyC..."
                helpText="Your Google Books API key"
              />
            </FormGroup>

            <div className={styles.buttonGroup}>
              <Button
                kind={kinds.SUCCESS}
                isDisabled={!hasChanges}
                onPress={this.onSavePress}
              >
                Save Settings
              </Button>

              <Button
                kind={kinds.DEFAULT}
                isDisabled={!hasApiKey || isTesting}
                onPress={this.onTestPress}
              >
                {isTesting ? (
                  <>
                    <LoadingIndicator size={16} />
                    Testing...
                  </>
                ) : (
                  'Test Connection'
                )}
              </Button>

              <Button
                kind={kinds.PRIMARY}
                onPress={this.onSetupWizardOpen}
              >
                <Icon name={icons.SETTINGS} />
                Setup Wizard
              </Button>
            </div>

            {testResult && (
              <div className={styles.testResult}>
                {testResult.isValid ? (
                  <Alert kind={kinds.SUCCESS}>
                    <Icon name={icons.CHECK} />
                    <strong>Connection Successful!</strong>
                    {testResult.testBookFound && (
                      <span> Found test book: "{testResult.testBookTitle}"</span>
                    )}
                  </Alert>
                ) : (
                  <Alert kind={kinds.DANGER}>
                    <Icon name={icons.DANGER} />
                    <strong>Connection Failed:</strong> {testResult.error}
                  </Alert>
                )}
              </div>
            )}
          </div>

          <GoogleBooksSetupWizard
            isOpen={isSetupWizardOpen}
            onModalClose={this.onSetupWizardClose}
            onCompleted={this.onSetupWizardCompleted}
          />
        </div>
      </FieldSet>
    );
  }
}

export default GoogleBooks;
