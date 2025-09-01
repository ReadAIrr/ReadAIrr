import React, { Component } from 'react';
import PropTypes from 'prop-types';
import { icons, kinds, sizes } from 'Helpers/Props';
import Alert from 'Components/Alert';
import Button from 'Components/Link/Button';
import CheckInput from 'Components/Form/CheckInput';
import FormGroup from 'Components/Form/FormGroup';
import FormInputGroup from 'Components/Form/FormInputGroup';
import FormLabel from 'Components/Form/FormLabel';
import Icon from 'Components/Icon';
import LoadingIndicator from 'Components/Loading/LoadingIndicator';
import Modal from 'Components/Modal/Modal';
import ModalContent from 'Components/Modal/ModalContent';
import ModalHeader from 'Components/Modal/ModalHeader';
import ModalBody from 'Components/Modal/ModalBody';
import ModalFooter from 'Components/Modal/ModalFooter';
import { inputTypes } from 'Helpers/Props';
import translate from 'Utilities/String/translate';
import styles from './GoogleBooksSetupWizard.css';

const WIZARD_STEPS = {
  WELCOME: 'welcome',
  GOOGLE_LOGIN: 'google_login',
  CREATE_PROJECT: 'create_project',
  ENABLE_API: 'enable_api',
  CREATE_API_KEY: 'create_api_key',
  VALIDATE_KEY: 'validate_key',
  COMPLETE: 'complete'
};

class GoogleBooksSetupWizard extends Component {

  //
  // Lifecycle

  constructor(props, context) {
    super(props, context);

    this.state = {
      currentStep: WIZARD_STEPS.WELCOME,
      setupData: null,
      apiKey: '',
      isValidating: false,
      validationResult: null,
      isCompleted: false,
      error: null
    };
  }

  componentDidMount() {
    if (this.props.isOpen) {
      this._initializeWizard();
    }
  }

  componentDidUpdate(prevProps) {
    if (!prevProps.isOpen && this.props.isOpen) {
      this._initializeWizard();
    }
  }

  //
  // Private Methods

  _initializeWizard = () => {
    this.setState({
      currentStep: WIZARD_STEPS.WELCOME,
      setupData: null,
      apiKey: '',
      isValidating: false,
      validationResult: null,
      isCompleted: false,
      error: null
    });
  }

  _getSetupUrls = async () => {
    try {
      const response = await fetch('/api/v1/config/googlebooks/setup-url');
      const data = await response.json();
      
      this.setState({
        setupData: data,
        currentStep: WIZARD_STEPS.CREATE_PROJECT
      });
    } catch (error) {
      this.setState({
        error: 'Failed to generate setup URLs. Please try again.'
      });
    }
  }

  _validateApiKey = async (apiKey) => {
    this.setState({
      isValidating: true,
      validationResult: null,
      error: null
    });

    try {
      const response = await fetch('/api/v1/config/googlebooks/validate', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json'
        },
        body: JSON.stringify({ apiKey })
      });

      const result = await response.json();
      
      this.setState({
        isValidating: false,
        validationResult: result
      });

      if (result.isValid) {
        // Save the configuration
        await this._saveConfiguration(apiKey);
        this.setState({ 
          currentStep: WIZARD_STEPS.COMPLETE,
          isCompleted: true
        });
      }
    } catch (error) {
      this.setState({
        isValidating: false,
        error: 'Failed to validate API key. Please check your internet connection and try again.'
      });
    }
  }

  _saveConfiguration = async (apiKey) => {
    try {
      await fetch('/api/v1/config/googlebooks/1', {
        method: 'PUT',
        headers: {
          'Content-Type': 'application/json'
        },
        body: JSON.stringify({
          id: 1,
          apiKey: apiKey,
          enabled: true
        })
      });
    } catch (error) {
      console.error('Failed to save Google Books configuration:', error);
    }
  }

  //
  // Event Handlers

  onStepForward = () => {
    const { currentStep } = this.state;
    
    switch (currentStep) {
      case WIZARD_STEPS.WELCOME:
        this._getSetupUrls();
        break;
      case WIZARD_STEPS.CREATE_PROJECT:
        this.setState({ currentStep: WIZARD_STEPS.ENABLE_API });
        break;
      case WIZARD_STEPS.ENABLE_API:
        this.setState({ currentStep: WIZARD_STEPS.CREATE_API_KEY });
        break;
      case WIZARD_STEPS.CREATE_API_KEY:
        this.setState({ currentStep: WIZARD_STEPS.VALIDATE_KEY });
        break;
      case WIZARD_STEPS.VALIDATE_KEY:
        if (this.state.apiKey.trim()) {
          this._validateApiKey(this.state.apiKey.trim());
        }
        break;
      default:
        break;
    }
  }

  onStepBack = () => {
    const { currentStep } = this.state;
    
    switch (currentStep) {
      case WIZARD_STEPS.CREATE_PROJECT:
        this.setState({ currentStep: WIZARD_STEPS.WELCOME });
        break;
      case WIZARD_STEPS.ENABLE_API:
        this.setState({ currentStep: WIZARD_STEPS.CREATE_PROJECT });
        break;
      case WIZARD_STEPS.CREATE_API_KEY:
        this.setState({ currentStep: WIZARD_STEPS.ENABLE_API });
        break;
      case WIZARD_STEPS.VALIDATE_KEY:
        this.setState({ currentStep: WIZARD_STEPS.CREATE_API_KEY });
        break;
      default:
        break;
    }
  }

  onApiKeyChange = ({ value }) => {
    this.setState({
      apiKey: value,
      validationResult: null,
      error: null
    });
  }

  onOpenUrl = (url) => {
    window.open(url, '_blank', 'noopener,noreferrer');
  }

  onClose = () => {
    if (this.state.isCompleted) {
      // Refresh the parent component to show new configuration
      if (this.props.onCompleted) {
        this.props.onCompleted();
      }
    }
    this.props.onModalClose();
  }

  //
  // Render Helpers

  _renderWelcomeStep() {
    return (
      <div className={styles.stepContent}>
        <div className={styles.welcomeIcon}>
          <Icon
            name={icons.GOOGLE}
            size={64}
          />
        </div>
        <h3>Setup Google Books API</h3>
        <p>
          Get audiobook duration data to validate the completeness of your audiobook collection.
          This setup takes about 2-3 minutes and requires a free Google account.
        </p>
        <div className={styles.featureList}>
          <div className={styles.feature}>
            <Icon name={icons.CHECK} kind={kinds.SUCCESS} />
            <span>Audiobook duration validation</span>
          </div>
          <div className={styles.feature}>
            <Icon name={icons.CHECK} kind={kinds.SUCCESS} />
            <span>Enhanced metadata from Google Books</span>
          </div>
          <div className={styles.feature}>
            <Icon name={icons.CHECK} kind={kinds.SUCCESS} />
            <span>Narrator information extraction</span>
          </div>
        </div>
      </div>
    );
  }

  _renderCreateProjectStep() {
    const { setupData } = this.state;
    
    return (
      <div className={styles.stepContent}>
        <h3>Step 1: Create Google Cloud Project</h3>
        <p>First, we'll create a new Google Cloud project for ReadAIrr.</p>
        
        <div className={styles.instructionBox}>
          <strong>Project Name:</strong> {setupData?.projectName}
        </div>

        <div className={styles.buttonGroup}>
          <Button
            kind={kinds.PRIMARY}
            size={sizes.LARGE}
            onPress={() => this.onOpenUrl(setupData?.projectCreationUrl)}
          >
            <Icon name={icons.EXTERNAL_LINK} />
            Create Project in Google Cloud
          </Button>
        </div>

        <Alert kind={kinds.INFO}>
          <strong>Instructions:</strong>
          <ol>
            <li>Click the button above to open Google Cloud Console</li>
            <li>Click "CREATE" to create the project</li>
            <li>Wait for the project to be created (30-60 seconds)</li>
            <li>Return here and click "Next Step"</li>
          </ol>
        </Alert>
      </div>
    );
  }

  _renderEnableApiStep() {
    const { setupData } = this.state;
    
    return (
      <div className={styles.stepContent}>
        <h3>Step 2: Enable Google Books API</h3>
        <p>Now we need to enable the Google Books API for your project.</p>

        <div className={styles.buttonGroup}>
          <Button
            kind={kinds.PRIMARY}
            size={sizes.LARGE}
            onPress={() => this.onOpenUrl(setupData?.enableApiUrl)}
          >
            <Icon name={icons.EXTERNAL_LINK} />
            Enable Google Books API
          </Button>
        </div>

        <Alert kind={kinds.INFO}>
          <strong>Instructions:</strong>
          <ol>
            <li>Click the button above to open the Google Books API page</li>
            <li>Make sure your project is selected in the top dropdown</li>
            <li>Click the "ENABLE" button</li>
            <li>Wait for the API to be enabled</li>
            <li>Return here and click "Next Step"</li>
          </ol>
        </Alert>
      </div>
    );
  }

  _renderCreateApiKeyStep() {
    const { setupData } = this.state;
    
    return (
      <div className={styles.stepContent}>
        <h3>Step 3: Create API Key</h3>
        <p>Finally, we'll create an API key to authenticate with Google Books.</p>

        <div className={styles.buttonGroup}>
          <Button
            kind={kinds.PRIMARY}
            size={sizes.LARGE}
            onPress={() => this.onOpenUrl(setupData?.createApiKeyUrl)}
          >
            <Icon name={icons.EXTERNAL_LINK} />
            Create API Key
          </Button>
        </div>

        <Alert kind={kinds.INFO}>
          <strong>Instructions:</strong>
          <ol>
            <li>Click the button above to open the Credentials page</li>
            <li>Click "+ CREATE CREDENTIALS" → "API key"</li>
            <li>Copy the generated API key</li>
            <li>Return here and paste the key in the next step</li>
          </ol>
        </Alert>

        <Alert kind={kinds.WARNING}>
          <strong>Important:</strong> Keep your API key secure and don't share it publicly. 
          You can restrict it to Google Books API only for better security.
        </Alert>
      </div>
    );
  }

  _renderValidateKeyStep() {
    const { apiKey, isValidating, validationResult, error } = this.state;
    
    return (
      <div className={styles.stepContent}>
        <h3>Step 4: Validate API Key</h3>
        <p>Paste your Google Books API key below to complete the setup.</p>

        <FormGroup>
          <FormLabel>Google Books API Key</FormLabel>
          <FormInputGroup
            type={inputTypes.PASSWORD}
            name="apiKey"
            value={apiKey}
            onChange={this.onApiKeyChange}
            placeholder="AIzaSyC..."
          />
        </FormGroup>

        {isValidating && (
          <div className={styles.validationStatus}>
            <LoadingIndicator size={20} />
            <span>Validating API key...</span>
          </div>
        )}

        {validationResult && !validationResult.isValid && (
          <Alert kind={kinds.DANGER}>
            {validationResult.error}
          </Alert>
        )}

        {error && (
          <Alert kind={kinds.DANGER}>
            {error}
          </Alert>
        )}
      </div>
    );
  }

  _renderCompleteStep() {
    const { validationResult } = this.state;
    
    return (
      <div className={styles.stepContent}>
        <div className={styles.successIcon}>
          <Icon
            name={icons.CHECK}
            kind={kinds.SUCCESS}
            size={64}
          />
        </div>
        <h3>Setup Complete!</h3>
        <p>
          Google Books API has been successfully configured for ReadAIrr.
          You can now enjoy enhanced audiobook metadata and duration validation.
        </p>

        {validationResult?.testBookFound && (
          <Alert kind={kinds.SUCCESS}>
            <strong>Test Successful:</strong> Found "{validationResult.testBookTitle}" in Google Books
          </Alert>
        )}

        <div className={styles.nextSteps}>
          <h4>What's Next?</h4>
          <ul>
            <li>Duration data will be automatically fetched for new books</li>
            <li>Audiobook completeness validation is now active</li>
            <li>You can test the integration in Settings → Metadata</li>
          </ul>
        </div>
      </div>
    );
  }

  _getCurrentStepNumber() {
    const stepNumbers = {
      [WIZARD_STEPS.WELCOME]: 0,
      [WIZARD_STEPS.CREATE_PROJECT]: 1,
      [WIZARD_STEPS.ENABLE_API]: 2,
      [WIZARD_STEPS.CREATE_API_KEY]: 3,
      [WIZARD_STEPS.VALIDATE_KEY]: 4,
      [WIZARD_STEPS.COMPLETE]: 5
    };
    
    return stepNumbers[this.state.currentStep] || 0;
  }

  _canGoForward() {
    const { currentStep, apiKey, isValidating } = this.state;
    
    if (isValidating) {
      return false;
    }
    
    if (currentStep === WIZARD_STEPS.VALIDATE_KEY) {
      return apiKey.trim().length > 0;
    }
    
    return currentStep !== WIZARD_STEPS.COMPLETE;
  }

  _canGoBack() {
    const { currentStep, isValidating } = this.state;
    
    return !isValidating && currentStep !== WIZARD_STEPS.WELCOME && currentStep !== WIZARD_STEPS.COMPLETE;
  }

  //
  // Render

  render() {
    const { isOpen } = this.props;
    const { currentStep, isCompleted } = this.state;
    
    const currentStepNumber = this._getCurrentStepNumber();
    const totalSteps = 5;
    
    return (
      <Modal
        isOpen={isOpen}
        onModalClose={this.onClose}
        size={sizes.LARGE}
      >
        <ModalContent onModalClose={this.onClose}>
          <ModalHeader>
            Google Books API Setup
            <div className={styles.progressBar}>
              <div 
                className={styles.progressFill} 
                style={{ width: `${(currentStepNumber / totalSteps) * 100}%` }}
              />
            </div>
          </ModalHeader>

          <ModalBody className={styles.wizardBody}>
            {currentStep === WIZARD_STEPS.WELCOME && this._renderWelcomeStep()}
            {currentStep === WIZARD_STEPS.CREATE_PROJECT && this._renderCreateProjectStep()}
            {currentStep === WIZARD_STEPS.ENABLE_API && this._renderEnableApiStep()}
            {currentStep === WIZARD_STEPS.CREATE_API_KEY && this._renderCreateApiKeyStep()}
            {currentStep === WIZARD_STEPS.VALIDATE_KEY && this._renderValidateKeyStep()}
            {currentStep === WIZARD_STEPS.COMPLETE && this._renderCompleteStep()}
          </ModalBody>

          <ModalFooter>
            <div className={styles.modalFooter}>
              <div className={styles.leftButtons}>
                {this._canGoBack() && (
                  <Button
                    onPress={this.onStepBack}
                  >
                    Back
                  </Button>
                )}
              </div>
              
              <div className={styles.rightButtons}>
                {currentStep === WIZARD_STEPS.COMPLETE ? (
                  <Button
                    kind={kinds.SUCCESS}
                    onPress={this.onClose}
                  >
                    Complete Setup
                  </Button>
                ) : (
                  <Button
                    kind={kinds.PRIMARY}
                    isDisabled={!this._canGoForward()}
                    onPress={this.onStepForward}
                  >
                    {currentStep === WIZARD_STEPS.WELCOME ? 'Get Started' : 
                     currentStep === WIZARD_STEPS.VALIDATE_KEY ? 'Validate & Complete' : 'Next Step'}
                  </Button>
                )}
                
                <Button
                  onPress={this.onClose}
                >
                  {isCompleted ? 'Done' : 'Cancel'}
                </Button>
              </div>
            </div>
          </ModalFooter>
        </ModalContent>
      </Modal>
    );
  }
}

GoogleBooksSetupWizard.propTypes = {
  isOpen: PropTypes.bool.isRequired,
  onModalClose: PropTypes.func.isRequired,
  onCompleted: PropTypes.func
};

GoogleBooksSetupWizard.defaultProps = {
  onCompleted: () => {}
};

export default GoogleBooksSetupWizard;
