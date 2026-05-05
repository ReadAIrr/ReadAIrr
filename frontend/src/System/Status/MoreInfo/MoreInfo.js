import React, { Component } from 'react';
import DescriptionList from 'Components/DescriptionList/DescriptionList';
import DescriptionListItemDescription from 'Components/DescriptionList/DescriptionListItemDescription';
import DescriptionListItemTitle from 'Components/DescriptionList/DescriptionListItemTitle';
import FieldSet from 'Components/FieldSet';
import Link from 'Components/Link/Link';
import translate from 'Utilities/String/translate';

class MoreInfo extends Component {

  //
  // Render

  render() {
    return (
      <FieldSet legend={translate('MoreInfo')}>
        <DescriptionList>
          <DescriptionListItemTitle>Home page</DescriptionListItemTitle>
          <DescriptionListItemDescription>
            <Link to="https://readairr.com/">readairr.com</Link>
          </DescriptionListItemDescription>

          <DescriptionListItemTitle>Upstream Wiki</DescriptionListItemTitle>
          <DescriptionListItemDescription>
            <Link to="https://wiki.servarr.com/readarr">Wiki</Link>
          </DescriptionListItemDescription>

          <DescriptionListItemTitle>Upstream Reddit</DescriptionListItemTitle>
          <DescriptionListItemDescription>
            <Link to="https://www.reddit.com/r/Readarr/">Readarr</Link>
          </DescriptionListItemDescription>

          <DescriptionListItemTitle>Upstream Discord</DescriptionListItemTitle>
          <DescriptionListItemDescription>
            <Link to="https://readarr.com/discord">Readarr on Discord</Link>
          </DescriptionListItemDescription>

          <DescriptionListItemTitle>Source</DescriptionListItemTitle>
          <DescriptionListItemDescription>
            <Link to="https://github.com/ReadAIrr/App/">github.com/ReadAIrr/App</Link>
          </DescriptionListItemDescription>

          <DescriptionListItemTitle>Feature Requests</DescriptionListItemTitle>
          <DescriptionListItemDescription>
            <Link to="https://github.com/ReadAIrr/App/issues">github.com/ReadAIrr/App/issues</Link>
          </DescriptionListItemDescription>

        </DescriptionList>
      </FieldSet>
    );
  }
}

MoreInfo.propTypes = {

};

export default MoreInfo;
