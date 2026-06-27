package com.afsarali.jab.client.model;

import com.fasterxml.jackson.annotation.JsonInclude;
import java.util.List;

@JsonInclude(JsonInclude.Include.NON_NULL)
public record LocatorSuggestion(
        String engine,
        String role,
        String roleEnUs,
        String name,
        String virtualAccessibleName,
        String description,
        String states,
        String statesEnUs,
        Integer indexInParent,
        Integer objectDepth,
        Integer childrenCount,
        String path,
        String indexPath,
        String xPath,
        String indexXPath,
        String semanticXPath,
        String parentRole,
        String parentName,
        Boolean hasManagedDescendantAncestor,
        List<String> actionNames,
        String textPreview,
        String textPreviewSource,
        Integer textCharCount,
        Integer textCaretIndex,
        Integer textIndexAtPoint,
        String textSelected,
        String textWord,
        String textSentence,
        String currentValue,
        String minimumValue,
        String maximumValue,
        ElementBounds bounds) {
}
