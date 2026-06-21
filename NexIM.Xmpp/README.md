NexIM XMPP Layer
==========

This project implements the XMPP adapting layer for _NexIM_.
This document serves as an overview of the XEP coverage, as well as
the particular choices made when XMPP stanzas are adapted to the server events.

## Feature support

The primary compliance target is the [XEP-0479: XMPP Compliance Suites 2023](https://xmpp.org/extensions/xep-0479.html).
Note that even client-side features are considered for support,
when the server's recognition of such features is necessary for proper representation in other protocols.

<table>

<tr><th colspan="3"><h4>Core Compliance Suite</h4></th></tr>
<tr><th>Specification</th><th>Feature</th><th>Support</th></tr>

<tr>
<th>
<a href="http://tools.ietf.org/html/rfc6120">RFC 6120</a>
</th>
<th>
Core features
</th>
<td>
✅
</td>
</tr>

<tr>
<th>
<a href="http://tools.ietf.org/html/rfc7590">RFC 7590</a>
</th>
<th>
TLS
</th>
<td>
✅
</td>
</tr>

<tr>
<th>
<a href="https://xmpp.org/extensions/xep-0368.html">XEP-0368</a>
</th>
<th>
Direct TLS
</th>
<td>
❌
</td>
</tr>

<tr>
<th>
<a href="https://xmpp.org/extensions/xep-0030.html">XEP-0030</a>
</th>
<th>
Feature discovery
</th>
<td>
✅
</td>
</tr>

<tr>
<th>
<a href="https://xmpp.org/extensions/xep-0115.html">XEP-0115</a>
</th>
<th>
Feature broadcasts
</th>
<td>
✅
</td>
</tr>

<tr>
<th>
<a href="https://xmpp.org/extensions/xep-0114.html">XEP-0114</a>
</th>
<th>
Server Extensibility
</th>
<td>
❌
</td>
</tr>

<tr>
<th>
<a href="https://xmpp.org/extensions/xep-0163.html">XEP-0163</a>
</th>
<th>
Event publishing
</th>
<td>
❌
</td>
</tr>

<tr><th colspan="3"><h4>Web Compliance Suite</h4></th></tr>
<tr><th>Specification</th><th>Feature</th><th>Support</th></tr>

<tr>
<th>
<a href="https://datatracker.ietf.org/doc/html/rfc7395">RFC 7395</a>
</th>
<th>
XMPP over WebSocket
</th>
<td>
✅
</td>
</tr>

<tr>
<th>
<a href="https://xmpp.org/extensions/xep-0206.html">XEP-0206</a>
</th>
<th>
XMPP over BOSH
</th>
<td>
❌
</td>
</tr>

<tr>
<th>
<a href="https://xmpp.org/extensions/xep-0156.html">XEP-0156</a>
</th>
<th>
Connection Mechanism Discovery
</th>
<td>
✅
</td>
</tr>

<tr><th colspan="3"><h4>IM Compliance Suite</h4></th></tr>
<tr><th>Specification</th><th>Feature</th><th>Support</th></tr>

<tr>
<th>
<a href="http://tools.ietf.org/html/rfc6121">RFC 6121</a>
</th>
<th>
Core features
</th>
<td>
✅
</td>
</tr>

<tr>
<th>
<a href="https://xmpp.org/extensions/xep-0245.html">XEP-0245</a>
</th>
<th>
The /me Command
</th>
<td>
❌
</td>
</tr>

<tr>
<th>
<a href="https://xmpp.org/extensions/xep-0054.html">XEP-0054</a>
</th>
<th>
vcard-temp
</th>
<td>
✅
</td>
</tr>

<tr>
<th>
<a href="https://xmpp.org/extensions/xep-0153.html">XEP-0153</a>
</th>
<th>
vCard-Based Avatars
</th>
<td>
✅
</td>
</tr>

<tr>
<th>
<a href="https://xmpp.org/extensions/xep-0084.html">XEP-0084</a>
</th>
<th>
User Avatars
</th>
<td>
❌
</td>
</tr>

<tr>
<th>
<a href="https://xmpp.org/extensions/xep-0398.html">XEP-0398</a>
</th>
<th>
User Avatar Compatibility
</th>
<td>
❌
</td>
</tr>

<tr>
<th>
<a href="https://xmpp.org/extensions/xep-0280.html">XEP-0280</a>
</th>
<th>
Outbound Message Synchronization
</th>
<td>
❌
</td>
</tr>

<tr>
<th>
<a href="https://xmpp.org/extensions/xep-0191.html">XEP-0191</a>
</th>
<th>
User Blocking
</th>
<td>
❌
</td>
</tr>

<tr>
<th>
<a href="https://xmpp.org/extensions/xep-0045.html">XEP-0045</a>
</th>
<th>
Group Chat
</th>
<td>
❌
</td>
</tr>

<tr>
<th>
<a href="https://xmpp.org/extensions/xep-0223.html">XEP-0223</a>
</th>
<th>
Persistent Storage of Private Data via PubSub
</th>
<td>
❌
</td>
</tr>

<tr>
<th>
<a href="https://xmpp.org/extensions/xep-0049.html">XEP-0049</a>
</th>
<th>
Private XML Storage
</th>
<td>
✅
</td>
</tr>

<tr>
<th>
<a href="https://xmpp.org/extensions/xep-0198.html">XEP-0198</a>
</th>
<th>
Stream Management
</th>
<td>
❌
</td>
</tr>

<tr>
<th>
<a href="https://xmpp.org/extensions/xep-0184.html">XEP-0184</a>
</th>
<th>
Message Acknowledgements
</th>
<td>
✅
</td>
</tr>

<tr>
<th>
<a href="https://xmpp.org/extensions/xep-0313.html">XEP-0313</a>
</th>
<th>
History Storage / Retrieval
</th>
<td>
❌
</td>
</tr>

<tr>
<th>
<a href="https://xmpp.org/extensions/xep-0085.html">XEP-0085</a>
</th>
<th>
Chat States
</th>
<td>
✅
</td>
</tr>

<tr>
<th>
<a href="https://xmpp.org/extensions/xep-0308.html">XEP-0308</a>
</th>
<th>
Message Correction
</th>
<td>
❌
</td>
</tr>

<tr>
<th>
<a href="https://xmpp.org/extensions/xep-0363.html">XEP-0363</a>
</th>
<th>
File Upload
</th>
<td>
❌
</td>
</tr>

<tr>
<th>
<a href="https://xmpp.org/extensions/xep-0234.html">XEP-0234</a>
</th>
<th>
Direct File Transfer
</th>
<td>
❌
</td>
</tr>

</table>

## Implementation notes

### XML processing

The XML parsing/formatting capabilities utilize .NET's XML encoder/decoder, so they
are fully usable for well-formed XML. The following specifics apply:

* Comments and processing instructions are ignored on input, and never produced.
* Character references are allowed to refer to all Unicode characters (including NUL).
* `>` is escaped in all positions.
* Whitespace within mixed content elements is preserved. Newline characters are encoded as character references.

### SASL

Currently, only the PLAIN mechanism is supported for SASL, with SCRAM support expected.

### `xml:lang` handling

It is strongly suggested by XMPP RFCs and reinforced by the XMPP-over-WebSocket specification,
that XML elements inherit the value of `xml:lang` from the elements that enclose them, and that the
`xml:lang` attribute on the opening XML stream tag serves just to provide the default language
for stanzas that don't hold `xml:lang`.
The implementation is consistent with this approach, and all stanzas sent over WebSocket
must always provide the `xml:lang` attribute explicitly (if not empty).

In addition, the model of every stanza must contain the `xml:lang` value in effect for the stanza.
The reason is that stanzas may contain additional XML data for unrecognized extensions, which
may depend on the value of `xml:lang` taken from the context, and it is not possible in such
situations to determine how to correct the `xml:lang` value when the stanza is re-sent
into a stream with a different language.
The `xml:lang` value is however omitted when the stanza is sent to a stream that already
has the proper `xml:lang` on its opening tag (in accordance with the XMPP specification).

### [XEP-0054: vcard-temp](https://xmpp.org/extensions/xep-0054.html)

Even though the DTD maintains a particular order of elements or mandatory elements, some clients do not
respect that. The server always produces vCard elements in the correct order, but accepts any order,
as is common with other schemas too.

It should be noted that many elements, especially `<PHOTO>`, may be present multiple times,
per the rules in [XEP-0292: vCard4 Over XMPP](https://xmpp.org/extensions/xep-0292.html).
This also makes it possible for distinct sessions to each have their own avatar.

The `<BDAY>` element allows for a truncated or incomplete date, pretty much a union of all XML date/time types
(`xs:time`, `xs:gYear`, `xs:gMonthDay`, etc.).

### [XEP-0033: Extended Stanza Addressing](https://xmpp.org/extensions/xep-0033.html)

The presence of an `<addresses>` element in the stanza containing addresses without `delivered="true"`
marks a multicast request. Since the eventing architecture has multicast built-in, there is no need
to run a distinct service specifically for multicast, thus it is _always_ the server itself
that is capable of accepting multicast messages. Conversely, any multicast message must be addressed
directly to the server itself, and not to any of the recipients or external multicast services,
with the only benevolent exception of allowing a multicast with a single recipient that matches the `to`
address of the stanza (thus requiring no additional processing on the recipient's part).
This is consistent with the algorithm outlined in XEP-0033 that prioritizes the local multicast service
over external ones.

Regular broadcast (generally of presence stanzas) may also utilize external multicast services
when routing stanzas to multiple recipients under the same external host. In that case,
an `<address type="bcc">` must be synthesized for each of the recipients.

### [XEP-0115: Entity Capabilities](https://xmpp.org/extensions/xep-0115.html)

The capability hashing algorithm in XEP-0115 is currently vague in terms of treating
the `<` character: on one hand, it mandates that occurrences of `&lt;` in the XML character data
must not be decoded to `<` (something every sane XML processor should uphold) to prevent attacks on
the format (which it does not achieve, since `&#60;` is also an encoding of `<`, for example),
yet it doesn't talk at all about occurrences of the actual `<` character, or how to handle escaping in general.
XML character data (as opposed to _parsed_ character data) is the textual content of an element/attribute
after all entity and character references were already substituted, which makes it quite easily able to contain
occurrences of `<`.
While it would be very strange to base the hash on how the particular text was _encoded_ in XML
(indeed, not all XML decoders make it possible to retrieve the original XML data that encoded the text),
the assumed intent of the specification is that some escaping is necessary; for that reason,
all characters are XML-encoded as if they were written to an XML stream, per .NET's default
XML encoding rules.

Another case of underspecification in XEP-0115 is the usage of `xml:lang` in the hashed capabilities,
which also contributes to differences in the resulting hash.
Implementations differ in where the value of `xml:lang` is taken from ‒ one view (held by Psi, for example)
is that it reflects only the immediate `xml:lang` attribute of the particular `<identity>` element,
and is empty if the attribute is missing.
However, in accordance with the usual XML usage (and implications from the XMPP RFCs),
this implementation holds the opposite view, in that `xml:lang` propagates from parent elements
to their descendants, thus the default value of `xml:lang` is taken from the `<iq>` stanza or the whole stream.
For this reason, the implementation distinguishes between explicit and implicit values of `xml:lang`,
depending on whether they were taken from the currently processed XML element, or any parent context,
and computes up to two hashes if the interpretation of implicit `xml:lang` would make a difference,
discerning what view the client holds.
For compatibility, the `xml:lang` attribute is always emitted on `<identity>` elements.
