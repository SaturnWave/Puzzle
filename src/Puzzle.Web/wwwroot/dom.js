// Element construction for the lab's page and the player page.

const SVG_NS = "http://www.w3.org/2000/svg";

/** An element with a class and text; text always goes in as text, never as markup. */
export function make(tag, className, text) {
  const element = document.createElement(tag);
  if (className) element.className = className;
  if (text !== undefined) element.textContent = text;
  return element;
}

export function svgElement(tag, attributes) {
  const element = document.createElementNS(SVG_NS, tag);
  for (const [name, value] of Object.entries(attributes)) element.setAttribute(name, String(value));
  return element;
}

export function svgText(text, attributes) {
  const element = svgElement("text", attributes);
  element.textContent = text;
  return element;
}
