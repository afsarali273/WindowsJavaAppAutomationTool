package com.afsarali.jab.client.model;

import com.fasterxml.jackson.annotation.JsonCreator;
import com.fasterxml.jackson.annotation.JsonInclude;
import com.fasterxml.jackson.annotation.JsonProperty;
import java.util.List;

@JsonInclude(JsonInclude.Include.NON_NULL)
public final class LocatorSuggestion {
    @JsonProperty private final String engine;
    @JsonProperty private final String role;
    @JsonProperty private final String roleEnUs;
    @JsonProperty private final String name;
    @JsonProperty private final String virtualAccessibleName;
    @JsonProperty private final String description;
    @JsonProperty private final String states;
    @JsonProperty private final String statesEnUs;
    @JsonProperty private final Integer indexInParent;
    @JsonProperty private final Integer objectDepth;
    @JsonProperty private final Integer childrenCount;
    @JsonProperty private final String path;
    @JsonProperty private final String indexPath;
    @JsonProperty private final String xPath;
    @JsonProperty private final String indexXPath;
    @JsonProperty private final String semanticXPath;
    @JsonProperty private final String parentRole;
    @JsonProperty private final String parentName;
    @JsonProperty private final Boolean hasManagedDescendantAncestor;
    @JsonProperty private final List<String> actionNames;
    @JsonProperty private final String textPreview;
    @JsonProperty private final String textPreviewSource;
    @JsonProperty private final Integer textCharCount;
    @JsonProperty private final Integer textCaretIndex;
    @JsonProperty private final Integer textIndexAtPoint;
    @JsonProperty private final String textSelected;
    @JsonProperty private final String textWord;
    @JsonProperty private final String textSentence;
    @JsonProperty private final String currentValue;
    @JsonProperty private final String minimumValue;
    @JsonProperty private final String maximumValue;
    @JsonProperty private final ElementBounds bounds;

    @JsonCreator
    public LocatorSuggestion(
            @JsonProperty("engine") String engine,
            @JsonProperty("role") String role,
            @JsonProperty("roleEnUs") String roleEnUs,
            @JsonProperty("name") String name,
            @JsonProperty("virtualAccessibleName") String virtualAccessibleName,
            @JsonProperty("description") String description,
            @JsonProperty("states") String states,
            @JsonProperty("statesEnUs") String statesEnUs,
            @JsonProperty("indexInParent") Integer indexInParent,
            @JsonProperty("objectDepth") Integer objectDepth,
            @JsonProperty("childrenCount") Integer childrenCount,
            @JsonProperty("path") String path,
            @JsonProperty("indexPath") String indexPath,
            @JsonProperty("xPath") String xPath,
            @JsonProperty("indexXPath") String indexXPath,
            @JsonProperty("semanticXPath") String semanticXPath,
            @JsonProperty("parentRole") String parentRole,
            @JsonProperty("parentName") String parentName,
            @JsonProperty("hasManagedDescendantAncestor") Boolean hasManagedDescendantAncestor,
            @JsonProperty("actionNames") List<String> actionNames,
            @JsonProperty("textPreview") String textPreview,
            @JsonProperty("textPreviewSource") String textPreviewSource,
            @JsonProperty("textCharCount") Integer textCharCount,
            @JsonProperty("textCaretIndex") Integer textCaretIndex,
            @JsonProperty("textIndexAtPoint") Integer textIndexAtPoint,
            @JsonProperty("textSelected") String textSelected,
            @JsonProperty("textWord") String textWord,
            @JsonProperty("textSentence") String textSentence,
            @JsonProperty("currentValue") String currentValue,
            @JsonProperty("minimumValue") String minimumValue,
            @JsonProperty("maximumValue") String maximumValue,
            @JsonProperty("bounds") ElementBounds bounds) {
        this.engine = engine;
        this.role = role;
        this.roleEnUs = roleEnUs;
        this.name = name;
        this.virtualAccessibleName = virtualAccessibleName;
        this.description = description;
        this.states = states;
        this.statesEnUs = statesEnUs;
        this.indexInParent = indexInParent;
        this.objectDepth = objectDepth;
        this.childrenCount = childrenCount;
        this.path = path;
        this.indexPath = indexPath;
        this.xPath = xPath;
        this.indexXPath = indexXPath;
        this.semanticXPath = semanticXPath;
        this.parentRole = parentRole;
        this.parentName = parentName;
        this.hasManagedDescendantAncestor = hasManagedDescendantAncestor;
        this.actionNames = actionNames;
        this.textPreview = textPreview;
        this.textPreviewSource = textPreviewSource;
        this.textCharCount = textCharCount;
        this.textCaretIndex = textCaretIndex;
        this.textIndexAtPoint = textIndexAtPoint;
        this.textSelected = textSelected;
        this.textWord = textWord;
        this.textSentence = textSentence;
        this.currentValue = currentValue;
        this.minimumValue = minimumValue;
        this.maximumValue = maximumValue;
        this.bounds = bounds;
    }

    public String engine() { return engine; }
    public String role() { return role; }
    public String roleEnUs() { return roleEnUs; }
    public String name() { return name; }
    public String virtualAccessibleName() { return virtualAccessibleName; }
    public String description() { return description; }
    public String states() { return states; }
    public String statesEnUs() { return statesEnUs; }
    public Integer indexInParent() { return indexInParent; }
    public Integer objectDepth() { return objectDepth; }
    public Integer childrenCount() { return childrenCount; }
    public String path() { return path; }
    public String indexPath() { return indexPath; }
    public String xPath() { return xPath; }
    public String indexXPath() { return indexXPath; }
    public String semanticXPath() { return semanticXPath; }
    public String parentRole() { return parentRole; }
    public String parentName() { return parentName; }
    public Boolean hasManagedDescendantAncestor() { return hasManagedDescendantAncestor; }
    public List<String> actionNames() { return actionNames; }
    public String textPreview() { return textPreview; }
    public String textPreviewSource() { return textPreviewSource; }
    public Integer textCharCount() { return textCharCount; }
    public Integer textCaretIndex() { return textCaretIndex; }
    public Integer textIndexAtPoint() { return textIndexAtPoint; }
    public String textSelected() { return textSelected; }
    public String textWord() { return textWord; }
    public String textSentence() { return textSentence; }
    public String currentValue() { return currentValue; }
    public String minimumValue() { return minimumValue; }
    public String maximumValue() { return maximumValue; }
    public ElementBounds bounds() { return bounds; }
}
