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
    @JsonProperty private final Boolean isTableLikeContainer;
    @JsonProperty private final Boolean isTableLikeRow;
    @JsonProperty private final Boolean isTableLikeCell;
    @JsonProperty private final String tableLikeKind;
    @JsonProperty private final String tableLikeContainerPath;
    @JsonProperty private final String tableLikeColumnHeader;
    @JsonProperty private final Integer tableLikeRowIndex;
    @JsonProperty private final Integer tableLikeColumnIndex;
    @JsonProperty private final Integer tableLikeRowCount;
    @JsonProperty private final Integer tableLikeColumnCount;
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
            @JsonProperty("isTableLikeContainer") Boolean isTableLikeContainer,
            @JsonProperty("isTableLikeRow") Boolean isTableLikeRow,
            @JsonProperty("isTableLikeCell") Boolean isTableLikeCell,
            @JsonProperty("tableLikeKind") String tableLikeKind,
            @JsonProperty("tableLikeContainerPath") String tableLikeContainerPath,
            @JsonProperty("tableLikeColumnHeader") String tableLikeColumnHeader,
            @JsonProperty("tableLikeRowIndex") Integer tableLikeRowIndex,
            @JsonProperty("tableLikeColumnIndex") Integer tableLikeColumnIndex,
            @JsonProperty("tableLikeRowCount") Integer tableLikeRowCount,
            @JsonProperty("tableLikeColumnCount") Integer tableLikeColumnCount,
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
        this.isTableLikeContainer = isTableLikeContainer;
        this.isTableLikeRow = isTableLikeRow;
        this.isTableLikeCell = isTableLikeCell;
        this.tableLikeKind = tableLikeKind;
        this.tableLikeContainerPath = tableLikeContainerPath;
        this.tableLikeColumnHeader = tableLikeColumnHeader;
        this.tableLikeRowIndex = tableLikeRowIndex;
        this.tableLikeColumnIndex = tableLikeColumnIndex;
        this.tableLikeRowCount = tableLikeRowCount;
        this.tableLikeColumnCount = tableLikeColumnCount;
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
    public Boolean isTableLikeContainer() { return isTableLikeContainer; }
    public Boolean isTableLikeRow() { return isTableLikeRow; }
    public Boolean isTableLikeCell() { return isTableLikeCell; }
    public String tableLikeKind() { return tableLikeKind; }
    public String tableLikeContainerPath() { return tableLikeContainerPath; }
    public String tableLikeColumnHeader() { return tableLikeColumnHeader; }
    public Integer tableLikeRowIndex() { return tableLikeRowIndex; }
    public Integer tableLikeColumnIndex() { return tableLikeColumnIndex; }
    public Integer tableLikeRowCount() { return tableLikeRowCount; }
    public Integer tableLikeColumnCount() { return tableLikeColumnCount; }
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

    public static Builder builder() {
        return new Builder();
    }

    public static LocatorSuggestion role(String role) {
        return builder().role(role).build();
    }

    public static LocatorSuggestion name(String name) {
        return builder().name(name).build();
    }

    public static LocatorSuggestion roleAndName(String role, String name) {
        return builder().role(role).name(name).build();
    }

    public static LocatorSuggestion virtualAccessibleName(String virtualAccessibleName) {
        return builder().virtualAccessibleName(virtualAccessibleName).build();
    }

    public static LocatorSuggestion description(String description) {
        return builder().description(description).build();
    }

    public static LocatorSuggestion path(String path) {
        return builder().path(path).build();
    }

    public static LocatorSuggestion indexPath(String indexPath) {
        return builder().indexPath(indexPath).build();
    }

    public static LocatorSuggestion xpath(String xPath) {
        return builder().xPath(xPath).build();
    }

    public static LocatorSuggestion semanticXPath(String semanticXPath) {
        return builder().semanticXPath(semanticXPath).build();
    }

    public static final class Builder {
        private String engine = "java-access-bridge";
        private String role;
        private String roleEnUs;
        private String name;
        private String virtualAccessibleName;
        private String description;
        private String states;
        private String statesEnUs;
        private Integer indexInParent;
        private Integer objectDepth;
        private Integer childrenCount;
        private String path;
        private String indexPath;
        private String xPath;
        private String indexXPath;
        private String semanticXPath;
        private String parentRole;
        private String parentName;
        private Boolean isTableLikeContainer;
        private Boolean isTableLikeRow;
        private Boolean isTableLikeCell;
        private String tableLikeKind;
        private String tableLikeContainerPath;
        private String tableLikeColumnHeader;
        private Integer tableLikeRowIndex;
        private Integer tableLikeColumnIndex;
        private Integer tableLikeRowCount;
        private Integer tableLikeColumnCount;
        private Boolean hasManagedDescendantAncestor;
        private List<String> actionNames;
        private String textPreview;
        private String textPreviewSource;
        private Integer textCharCount;
        private Integer textCaretIndex;
        private Integer textIndexAtPoint;
        private String textSelected;
        private String textWord;
        private String textSentence;
        private String currentValue;
        private String minimumValue;
        private String maximumValue;
        private ElementBounds bounds;

        public Builder engine(String engine) { this.engine = engine; return this; }
        public Builder role(String role) { this.role = role; return this; }
        public Builder roleEnUs(String roleEnUs) { this.roleEnUs = roleEnUs; return this; }
        public Builder name(String name) { this.name = name; return this; }
        public Builder virtualAccessibleName(String virtualAccessibleName) { this.virtualAccessibleName = virtualAccessibleName; return this; }
        public Builder description(String description) { this.description = description; return this; }
        public Builder states(String states) { this.states = states; return this; }
        public Builder statesEnUs(String statesEnUs) { this.statesEnUs = statesEnUs; return this; }
        public Builder indexInParent(Integer indexInParent) { this.indexInParent = indexInParent; return this; }
        public Builder objectDepth(Integer objectDepth) { this.objectDepth = objectDepth; return this; }
        public Builder childrenCount(Integer childrenCount) { this.childrenCount = childrenCount; return this; }
        public Builder path(String path) { this.path = path; return this; }
        public Builder indexPath(String indexPath) { this.indexPath = indexPath; return this; }
        public Builder xPath(String xPath) { this.xPath = xPath; return this; }
        public Builder indexXPath(String indexXPath) { this.indexXPath = indexXPath; return this; }
        public Builder semanticXPath(String semanticXPath) { this.semanticXPath = semanticXPath; return this; }
        public Builder parentRole(String parentRole) { this.parentRole = parentRole; return this; }
        public Builder parentName(String parentName) { this.parentName = parentName; return this; }
        public Builder isTableLikeContainer(Boolean isTableLikeContainer) { this.isTableLikeContainer = isTableLikeContainer; return this; }
        public Builder isTableLikeRow(Boolean isTableLikeRow) { this.isTableLikeRow = isTableLikeRow; return this; }
        public Builder isTableLikeCell(Boolean isTableLikeCell) { this.isTableLikeCell = isTableLikeCell; return this; }
        public Builder tableLikeKind(String tableLikeKind) { this.tableLikeKind = tableLikeKind; return this; }
        public Builder tableLikeContainerPath(String tableLikeContainerPath) { this.tableLikeContainerPath = tableLikeContainerPath; return this; }
        public Builder tableLikeColumnHeader(String tableLikeColumnHeader) { this.tableLikeColumnHeader = tableLikeColumnHeader; return this; }
        public Builder tableLikeRowIndex(Integer tableLikeRowIndex) { this.tableLikeRowIndex = tableLikeRowIndex; return this; }
        public Builder tableLikeColumnIndex(Integer tableLikeColumnIndex) { this.tableLikeColumnIndex = tableLikeColumnIndex; return this; }
        public Builder tableLikeRowCount(Integer tableLikeRowCount) { this.tableLikeRowCount = tableLikeRowCount; return this; }
        public Builder tableLikeColumnCount(Integer tableLikeColumnCount) { this.tableLikeColumnCount = tableLikeColumnCount; return this; }
        public Builder hasManagedDescendantAncestor(Boolean hasManagedDescendantAncestor) { this.hasManagedDescendantAncestor = hasManagedDescendantAncestor; return this; }
        public Builder actionNames(List<String> actionNames) { this.actionNames = actionNames; return this; }
        public Builder textPreview(String textPreview) { this.textPreview = textPreview; return this; }
        public Builder textPreviewSource(String textPreviewSource) { this.textPreviewSource = textPreviewSource; return this; }
        public Builder textCharCount(Integer textCharCount) { this.textCharCount = textCharCount; return this; }
        public Builder textCaretIndex(Integer textCaretIndex) { this.textCaretIndex = textCaretIndex; return this; }
        public Builder textIndexAtPoint(Integer textIndexAtPoint) { this.textIndexAtPoint = textIndexAtPoint; return this; }
        public Builder textSelected(String textSelected) { this.textSelected = textSelected; return this; }
        public Builder textWord(String textWord) { this.textWord = textWord; return this; }
        public Builder textSentence(String textSentence) { this.textSentence = textSentence; return this; }
        public Builder currentValue(String currentValue) { this.currentValue = currentValue; return this; }
        public Builder minimumValue(String minimumValue) { this.minimumValue = minimumValue; return this; }
        public Builder maximumValue(String maximumValue) { this.maximumValue = maximumValue; return this; }
        public Builder bounds(ElementBounds bounds) { this.bounds = bounds; return this; }

        public LocatorSuggestion build() {
            return new LocatorSuggestion(
                    engine,
                    role,
                    roleEnUs,
                    name,
                    virtualAccessibleName,
                    description,
                    states,
                    statesEnUs,
                    indexInParent,
                    objectDepth,
                    childrenCount,
                    path,
                    indexPath,
                    xPath,
                    indexXPath,
                    semanticXPath,
                    parentRole,
                    parentName,
                    isTableLikeContainer,
                    isTableLikeRow,
                    isTableLikeCell,
                    tableLikeKind,
                    tableLikeContainerPath,
                    tableLikeColumnHeader,
                    tableLikeRowIndex,
                    tableLikeColumnIndex,
                    tableLikeRowCount,
                    tableLikeColumnCount,
                    hasManagedDescendantAncestor,
                    actionNames,
                    textPreview,
                    textPreviewSource,
                    textCharCount,
                    textCaretIndex,
                    textIndexAtPoint,
                    textSelected,
                    textWord,
                    textSentence,
                    currentValue,
                    minimumValue,
                    maximumValue,
                    bounds);
        }
    }
}
